# Firefox Extension - Download Link Not Sent to Main XDM App

## Issue Summary

**Status**: FIXED - Ready for Testing
**Severity**: High - Breaks browser integration functionality
**Date Identified**: 2026-10-12
**Date Fixed**: 2026-10-12
**Fix Version**: 8.1

The Firefox browser extension successfully detects videos and displays them in the popup, but clicking a video doesn't trigger the XDM download dialog.

## Symptom

1. Extension loads successfully (version 8.0)
2. Videos are detected and appear in extension popup
3. Clicking a video in the popup does nothing - no download dialog appears in XDM
4. Manual downloads (right-click context menu) work correctly

## Root Cause Analysis

### The Expected Flow

1. **Video Detection Phase**:
   - `RequestWatcher.onHeadersReceivedEvent()` detects video (WORKING)
   - Calls `App.onRequestDataReceived()` with full video metadata (URL, headers, cookies)
   - Should POST to XDM's `/media` endpoint with complete data (NOT WORKING)
   - XDM's VideoTracker stores video with generated GUID
   - Next `/sync` poll returns video list to extension

2. **User Click Phase**:
   - User clicks video in popup
   - `popup.js` sends message `{type: "vid", itemId: <video-id>}` (WORKING)
   - `app.js` POSTs to XDM's `/vid` endpoint with video ID (WORKING)
   - XDM looks up video ID in VideoTracker dictionaries (FAILS - video not found)
   - XDM should show download dialog

### Why It Fails

**Problem**: Video metadata never gets sent to XDM during detection phase.

**Location**: `app/XDM/firefox-amo/app/app.js` lines 59-70

```javascript
onRequestDataReceived(data) {
    //Streaming video data received, send to native messaging application
    this.logger.log("onRequestDataReceived");
    this.logger.log(data);
    if (this.isMonitoringEnabled() && this.connector.isConnected()) {
        if (data.download) {
            this.connector.postMessage("/download", data);
        } else {
            this.connector.postMessage("/media", data);
        }
    }
}
```

**Condition Check** (line 63): Requires BOTH:
1. `isMonitoringEnabled()` returns true
2. `connector.isConnected()` returns true

**isMonitoringEnabled()** (lines 54-57):
```javascript
isMonitoringEnabled() {
    this.logger.log(this.appEnabled + " " + this.userDisabled);
    return this.appEnabled === true && this.userDisabled === false && this.connector.isConnected();
}
```

**Connector Connection State** (`app/XDM/firefox-amo/app/connector.js`):
- Line 10: `this.connected = undefined;` - starts as **undefined**
- Line 15: `setInterval(this.onTimer.bind(this), 5000);` - polls every 5 seconds
- Line 34: `this.connected = true;` - only set after first successful `/sync` response

### The Race Condition

**Timeline**:
1. Extension loads → `connector.connected = undefined`
2. Page loads, videos start loading → `RequestWatcher` detects video
3. Calls `onRequestDataReceived()` → checks `connector.isConnected()` → returns `undefined` (falsy)
4. Video metadata **NOT sent** to XDM
5. 0-5 seconds later → first `/sync` completes → `connector.connected = true`
6. Extension now connected but video metadata already lost

**Result**: The extension's videoList only contains old videos from XDM's database (previous sessions), not newly detected videos.

## Why Videos Appear in Popup

The videos the user sees in the popup are **old videos** from previous XDM sessions that are still in XDM's database. When `/sync` returns `msg.videoList` (line 37 in app.js), this is XDM's stored video list, not newly detected videos.

This explains why:
- Videos appear in popup (they're old videos)
- Clicking them works sometimes (if video ID still valid in XDM)
- Clicking them fails for expired videos (XDM has the display info but link may be expired)

## File References

### Extension Files
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/app.js:59-70` - onRequestDataReceived (broken)
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/app.js:54-57` - isMonitoringEnabled check
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/connector.js:10` - connected state initialization
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/connector.js:34` - connected state set to true
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/request-watcher.js:104-137` - Video detection logic
- `/Users/minhtran/Git_Packages/xdm/app/XDM/firefox-amo/app/popup.js:65-68` - Click handler (fixed)

### XDM Server Files
- `/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Core/BrowserMonitoring/IpcHttpMessageProcessor.cs:58-59` - /media endpoint
- `/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Core/BrowserMonitoring/IpcHttpMessageProcessor.cs:64-66` - /vid endpoint
- `/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Core/BrowserMonitoring/VideoTracker.cs:313-370` - AddVideoDownload (lookup by ID)

## Solution Options

### Option 1: Queue Videos Until Connected (Recommended)

Modify `app.js` to queue detected videos and send them once connected:

```javascript
constructor() {
    // ... existing code ...
    this.pendingVideos = []; // Add queue
}

onRequestDataReceived(data) {
    this.logger.log("onRequestDataReceived");
    this.logger.log(data);

    if (this.isMonitoringEnabled() && this.connector.isConnected()) {
        // Send immediately if connected
        if (data.download) {
            this.connector.postMessage("/download", data);
        } else {
            this.connector.postMessage("/media", data);
        }
    } else if (this.appEnabled === true && this.userDisabled === false) {
        // Queue for later if monitoring enabled but not connected yet
        this.pendingVideos.push(data);
    }
}

onMessage(msg) {
    // ... existing code ...

    // Flush pending videos when connected
    if (this.pendingVideos.length > 0 && this.connector.isConnected()) {
        this.pendingVideos.forEach(data => {
            if (data.download) {
                this.connector.postMessage("/download", data);
            } else {
                this.connector.postMessage("/media", data);
            }
        });
        this.pendingVideos = [];
    }
}
```

**Pros**:
- Captures all videos, even those detected before connection
- No videos lost
- Minimal code change

**Cons**:
- Uses memory for pending queue
- Videos detected on other tabs before extension connected might be stale

### Option 2: Eager Connection on Extension Load

Modify `connector.js` to attempt immediate connection:

```javascript
connect() {
    // Immediate connection attempt
    this.onTimer();
    // Then continue with periodic polling
    setInterval(this.onTimer.bind(this), 5000);
}
```

**Pros**:
- Simple one-line change
- Faster initial connection

**Cons**:
- Still a race condition (video might be detected before first response)
- Doesn't fully solve the problem

### Option 3: Retry Failed Sends (Hybrid)

Combine Option 1 and 2 - eager connection + queue for retry:

```javascript
onRequestDataReceived(data) {
    this.logger.log("onRequestDataReceived");
    this.logger.log(data);

    // Always try to send if monitoring enabled
    if (this.isMonitoringEnabled()) {
        if (this.connector.isConnected()) {
            // Send immediately
            if (data.download) {
                this.connector.postMessage("/download", data);
            } else {
                this.connector.postMessage("/media", data);
            }
        } else {
            // Queue for when connected
            this.pendingVideos.push(data);
        }
    }
}
```

**Pros**:
- Best of both worlds
- Guarantees no video loss

**Cons**:
- Slightly more complex

## Recommended Fix

Implement **Option 3 (Hybrid)** with the following changes:

1. **connector.js**: Add immediate connection attempt
2. **app.js**: Add pending queue and flush logic
3. **Test**: Verify videos detected during page load are captured

## Testing Procedure

After fix is implemented:

1. **Close XDM app completely**
2. **Load Firefox extension**
3. **Immediately navigate to YouTube and play a video**
4. **While video is loading, start XDM app**
5. **Check extension popup** - should see newly detected video
6. **Click video** - XDM should show download dialog
7. **Verify in extension console**:
   ```javascript
   app.connector.connected // should be true
   app.pendingVideos.length // should be 0 after flush
   ```
8. **Check XDM logs** for `/media` POST requests

## Workaround for Users

Until fixed, users can:
1. Ensure XDM is running **before** opening browser
2. Refresh the page if video not detected
3. Use "Download with XDM" right-click context menu instead of popup

## Related Issues

- **Popup click bug** (FIXED) - Changed `e.target.id` to `id` in popup.js:66
- **Version tracking** - Extension now at v8.0 per user request

## Current Testing Status (2026-10-12 Session 2)

### What We Discovered

1. **Extension Popup → Background Communication: WORKING ✅**
   - Popup correctly sends `{type: "vid", itemId: "<video-id>"}` when user clicks
   - Confirmed in background console: `MESSAGE RECEIVED: Object { type: "vid", itemId: "652c5071-0d50-42bb-9b1a-be7e2bb44486" }`

2. **Extension → XDM HTTP Communication: WORKING ✅**
   - Fetch to `http://127.0.0.1:8597/vid` succeeds with HTTP 200
   - Confirmed with manual test:
     ```javascript
     fetch("http://127.0.0.1:8597/vid", {
       method: "POST",
       headers: {"Content-Type": "application/json"},
       body: JSON.stringify({vid: app.videoList[0].id})
     }).then(r => r.json()).then(console.log);
     ```
   - Response received: Full JSON config with videoList
   - Curl test also succeeded: `curl -X POST http://127.0.0.1:8597/vid -H "Content-Type: application/json" -d '{"vid":"..."}'`

3. **XDM /vid Endpoint Processing: SILENTLY FAILING ❌**
   - XDM receives the POST request (returns 200 OK)
   - But does NOT show download dialog
   - No errors in logs
   - **Root Cause**: Video ID from extension not found in XDM's internal video dictionaries

### Why Videos Aren't Found

The issue is a **lifecycle mismatch**:

1. Extension polls `/sync` → Gets list of video IDs that XDM stored in DATABASE
2. User clicks video → Extension sends video ID to `/vid`
3. XDM looks up video ID in **IN-MEMORY dictionaries** (not database!)
4. If video was from a previous session or XDM was restarted, it's only in database, not in memory
5. Lookup fails → `valid = false` → No dialog shown

**Code Location**: `VideoTracker.cs:313-370`
```csharp
public void AddVideoDownload(string videoId)
{
    // Searches 4 in-memory dictionaries:
    if (ytVideoList.ContainsKey(videoId)) { ... }
    else if (videoList.ContainsKey(videoId)) { ... }
    else if (hlsVideoList.ContainsKey(videoId)) { ... }
    else if (dashVideoList.ContainsKey(videoId)) { ... }

    if (valid) {
        // Show dialog
    }
    // If not found: silently does nothing!
}
```

### Debug Logging Added

Added extensive logging to `VideoTracker.cs` to diagnose the issue:

```csharp
Log.Debug($"[VID] Received video download request for ID: {videoId}");
Log.Debug($"[VID] ytVideoList count: {ytVideoList.Count}, videoList count: {videoList.Count}...");

if (valid) {
    Log.Debug($"[VID] Video found! Name: {name}, Size: {size}, Type: {contentType}");
    Log.Debug("[VID] Showing video download dialog");
} else {
    Log.Debug($"[VID] ERROR: Video ID '{videoId}' not found in any video list!");
    Log.Debug($"[VID] Total videos in memory: yt={ytVideoList.Count}, video={videoList.Count}...");
}
```

**Status**: Code modified, build successful, but needs testing with newly built XDM binary.

### Testing Blockers

- User is running XDM from `/Applications/XDM.app` (installed version)
- Need to either:
  1. Run from build directory: `/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Gtk.UI.macOS/bin/Release/net9.0/osx-arm64/xdm-app`
  2. Rebuild DMG and reinstall
  3. Copy built binary to `/Applications/XDM.app/Contents/MacOS/`

### Next Steps for New Session

1. **Deploy newly built XDM with debug logging**:
   ```bash
   killall xdm-app
   /Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Gtk.UI.macOS/bin/Release/net9.0/osx-arm64/xdm-app > /tmp/xdm-debug.log 2>&1 &
   ```

2. **Test with FRESH video detection**:
   - Navigate to a NEW video site (not YouTube, per user preference)
   - Extension should detect video → POST to `/media`
   - Wait 5 seconds for XDM to store it
   - Click video in popup
   - Check logs: `tail -f /tmp/xdm-debug.log | grep "\[VID\]"`

3. **Verify fix works**: Should see logs showing video was found and dialog displayed

4. **If still failing**: Check if video was successfully sent to `/media` endpoint when detected

## Implementation Details (COMPLETED)

**Date**: 2026-10-12
**Version**: 8.1

### Changes Made

#### Firefox Extension (`app/XDM/firefox-amo/`)

1. **connector.js** - Added immediate connection attempt:
   ```javascript
   connect() {
       //This will be replaced with websocket in future versions
       //Try immediate connection to reduce race condition window
       this.onTimer();  // <-- ADDED
       setInterval(this.onTimer.bind(this), 5000);
   }
   ```

2. **app.js** - Added pending queue:
   ```javascript
   constructor() {
       // ... existing code ...
       this.pendingVideos = []; // <-- ADDED
   }
   ```

3. **app.js** - Modified `onRequestDataReceived()`:
   ```javascript
   onRequestDataReceived(data) {
       this.logger.log("onRequestDataReceived");
       this.logger.log(data);

       // Check if monitoring is enabled (user preference and app config)
       if (this.appEnabled === true && this.userDisabled === false) {
           if (this.connector.isConnected()) {
               // Send immediately if already connected
               if (data.download) {
                   this.connector.postMessage("/download", data);
               } else {
                   this.connector.postMessage("/media", data);
               }
           } else {
               // Queue for later if not connected yet (race condition during startup)
               this.logger.log("Not connected yet, queueing video for later");
               this.pendingVideos.push(data);
           }
       }
   }
   ```

4. **app.js** - Modified `onMessage()` to flush queue:
   ```javascript
   onMessage(msg) {
       // ... existing code ...

       // Flush pending videos that were detected before connection was established
       if (this.pendingVideos.length > 0 && this.connector.isConnected()) {
           this.logger.log("Flushing " + this.pendingVideos.length + " pending videos");
           this.pendingVideos.forEach(data => {
               if (data.download) {
                   this.connector.postMessage("/download", data);
               } else {
                   this.connector.postMessage("/media", data);
               }
           });
           this.pendingVideos = [];
       }
   }
   ```

5. **manifest.json** - Updated version from 8.0 to 8.1

#### Chrome Extension (`app/XDM/chrome-extension/`)

Applied identical fixes to Chrome extension with appropriate syntax for Manifest v3.

### Build Status

- Extensions built successfully
- Build output: `/Users/minhtran/Git_Packages/xdm/app/XDM/XDM.Gtk.UI.macOS/bin/Release/net9.0/osx-arm64/`
- Both Firefox and Chrome extensions updated to v8.1

## Next Steps

1. ✅ ~~Implement Option 3 fix in app.js and connector.js~~
2. ✅ ~~Rebuild extension~~
3. ✅ ~~Test with fresh browser session~~
4. ✅ ~~Update both Firefox and Chrome extensions (same bug affects both)~~
5. ✅ ~~Update extension version to 8.2 (with debug logging)~~

## Update: 2026-10-14 - macOS Dialog Display Issue Fixed

### Issue Discovery

After implementing the race condition fix (v8.1), testing on macOS revealed a **new issue**: the download dialog was not appearing when clicking videos in the extension popup.

#### Investigation Process

1. **Backend Verification** ✅
   - Extension successfully sends `/vid` POST request
   - XDM receives request and finds video in `videoList`
   - Video metadata (URL, headers, size) correctly stored
   - Log shows: `[VID] Showing video download dialog`

2. **Dialog Creation Failure** ❌
   - `Application.ShowVideoDownloadDialog()` is called
   - `RunOnUiThread()` executes
   - **Crashes when creating dialog**: `GException: Invalid byte sequence in conversion input`
   - Error occurs in: `GLib.Marshaller.StringToFilenamePtr(String str)`
   - Specifically fails at: `builder.AddFromFile(gladePath)`

#### Root Cause

**GTK encoding issue on macOS**: GTK's `GLib.Marshaller.StringToFilenamePtr()` cannot handle the file path encoding when loading `.glade` files via `builder.AddFromFile()` on macOS, even though the file exists and path is valid.

**Error Details:**
```
File: /Applications/XDM.app/Contents/MacOS/glade/new-video-download-window.glade
File exists: True
Error: GException: Invalid byte sequence in conversion input
      at GLib.Marshaller.StringToFilenamePtr(String str)
```

#### Solution Implemented

**File**: `XDM.Gtk.UI.macOS/Dialogs/NewVideoDownload/NewVideoDownloadWindow.cs:58`

Changed from path-based loading to string-based loading:

```csharp
// OLD (fails on macOS):
builder.AddFromFile(gladePath);

// NEW (works on macOS):
var gladeContent = System.IO.File.ReadAllText(gladePath);
builder.AddFromString(gladeContent);
```

This bypasses GTK's problematic path encoding marshaller while achieving the same result.

#### Files Modified

- `XDM.Gtk.UI.macOS/Dialogs/NewVideoDownload/NewVideoDownloadWindow.cs` - Fixed glade loading
- `XDM.Core/Application.cs` - Added dialog flow logging
- `XDM.Core/UI/NewVideoDownloadDialogUIController.cs` - Added controller logging
- `XDM.Core/BrowserMonitoring/VideoTracker.cs` - Added video data logging
- `firefox-amo/manifest.json` - Version 8.2
- `chrome-extension/manifest.json` - Version 8.2

#### Testing Status

- Extension v8.2: ✅ Race condition fixed, videos queued until connected
- Backend: ✅ Videos detected, stored, and retrieved correctly
- Dialog: ⏳ Testing fix for GTK encoding issue

#### Install Script Created

Created `/tmp/install-xdm.sh` for consistent builds and deployment:
```bash
/tmp/install-xdm.sh  # Builds, installs to /Applications/XDM.app, starts with XDM_DEBUG_MODE=1
```
