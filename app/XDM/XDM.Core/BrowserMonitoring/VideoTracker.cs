using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TraceLog;
using XDM.Core.Collections;
using XDM.Core.Downloader;
using XDM.Core.Downloader.Adaptive.Dash;
using XDM.Core.Downloader.Adaptive.Hls;
using XDM.Core.Downloader.Progressive.DualHttp;
using XDM.Core.Downloader.Progressive.SingleHttp;
using XDM.Core.MediaProcessor;
using XDM.Core.Util;

namespace XDM.Core.BrowserMonitoring
{
    public class VideoTracker : IVideoTracker
    {
        private GenericOrderedDictionary<string, KeyValuePair<DualSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>> ytVideoList = new();
        private GenericOrderedDictionary<string, KeyValuePair<SingleSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>> videoList = new();
        private GenericOrderedDictionary<string, KeyValuePair<MultiSourceHLSDownloadInfo, StreamingVideoDisplayInfo>> hlsVideoList = new();
        private GenericOrderedDictionary<string, KeyValuePair<MultiSourceDASHDownloadInfo, StreamingVideoDisplayInfo>> dashVideoList = new();

        public event EventHandler<MediaInfoEventArgs> MediaAdded;
        public event EventHandler<MediaInfoEventArgs> MediaUpdated;

        public void ClearVideoList()
        {
            ytVideoList.Clear();
            hlsVideoList.Clear();
            dashVideoList.Clear();
            videoList.Clear();
            ApplicationContext.BroadcastConfigChange();
        }

        private string GenerateUpdatedFileName(string oldFile, string newName)
        {
            var ext = Path.GetExtension(oldFile);
            var file = FileHelper.SanitizeFileName(newName);
            if (!string.IsNullOrEmpty(ext))
            {
                file += ext;
            }
            return file!;
        }

        public void UpdateMediaTitle(string tabUrl, string tabTitle)
        {
            foreach (var e in ytVideoList)
            {
                var u = e.Value.Value.TabUrl;
                if (string.IsNullOrEmpty(u))
                {
                    continue;
                }
                if (u == tabUrl)
                {
                    e.Value.Key.File = GenerateUpdatedFileName(e.Value.Key.File, tabTitle);
                    this.MediaUpdated?.Invoke(
                        this,
                        new MediaInfoEventArgs
                        {
                            MediaInfo = new MediaInfo(e.Key, e.Value.Key.File,
                            e.Value.Value.DescriptionText, e.Value.Value.CreationTime,
                            e.Value.Value.TabId)
                        });
                }
            }

            foreach (var e in videoList)
            {
                var u = e.Value.Value.TabUrl;
                if (string.IsNullOrEmpty(u))
                {
                    continue;
                }
                if (u == tabUrl)
                {
                    e.Value.Key.File = GenerateUpdatedFileName(e.Value.Key.File, tabTitle);
                    this.MediaUpdated?.Invoke(
                        this,
                        new MediaInfoEventArgs
                        {
                            MediaInfo = new MediaInfo(e.Key, e.Value.Key.File, e.Value.Value.DescriptionText,
                            e.Value.Value.CreationTime, e.Value.Value.TabId)
                        });
                }
            }
        }

        public bool IsFFmpegRequiredForDownload(string id)
        {
            return ytVideoList.ContainsKey(id) || dashVideoList.ContainsKey(id) || hlsVideoList.ContainsKey(id);
        }

        public void StartVideoDownload(string videoId,
            string name,
            string? folder,
            bool startImmediately,
            AuthenticationInfo? authentication,
            ProxyInfo? proxyInfo,
            int maxSpeedLimit,
            string? queueId,
            bool convertToMp3 = false //only applicable for dual source http downloads
            )
        {
            //IBaseDownloader downloader = null;
            if (ytVideoList.ContainsKey(videoId))
            {
                ApplicationContext.CoreService.StartDownload(ytVideoList[videoId].Key, name, FileNameFetchMode.ExtensionOnly,
                        folder, startImmediately, authentication, proxyInfo, queueId, false);
            }
            else if (videoList.ContainsKey(videoId))
            {
                ApplicationContext.CoreService.StartDownload(videoList[videoId].Key, name, convertToMp3 ? FileNameFetchMode.None : FileNameFetchMode.ExtensionOnly,
                    folder, startImmediately, authentication, proxyInfo, queueId, convertToMp3);
            }
            else if (hlsVideoList.ContainsKey(videoId))
            {
                ApplicationContext.CoreService.StartDownload(hlsVideoList[videoId].Key, name, FileNameFetchMode.ExtensionOnly,
                    folder, startImmediately, authentication, proxyInfo, queueId, false);
            }
            else if (dashVideoList.ContainsKey(videoId))
            {
                ApplicationContext.CoreService.StartDownload(dashVideoList[videoId].Key, name, FileNameFetchMode.ExtensionOnly,
                    folder, startImmediately, authentication, proxyInfo, queueId, false);
            }
        }

        public List<MediaInfo> GetVideoList()
        {
            lock (this)
            {
                var list = new List<MediaInfo>();
                foreach (var e in ytVideoList)
                {
                    list.Add(new MediaInfo(e.Key, e.Value.Key.File, e.Value.Value.DescriptionText,
                        e.Value.Value.CreationTime, e.Value.Value.TabId));
                }
                foreach (var e in videoList)
                {
                    list.Add(new MediaInfo(e.Key, e.Value.Key.File, e.Value.Value.DescriptionText,
                        e.Value.Value.CreationTime, e.Value.Value.TabId));
                }
                foreach (var e in hlsVideoList)
                {
                    list.Add(new MediaInfo(e.Key, e.Value.Key.File, e.Value.Value.DescriptionText,
                        e.Value.Value.CreationTime, e.Value.Value.TabId));
                }
                foreach (var e in dashVideoList)
                {
                    list.Add(new MediaInfo(e.Key, e.Value.Key.File, e.Value.Value.DescriptionText,
                        e.Value.Value.CreationTime, e.Value.Value.TabId));
                }
                list.Sort((a, b) => a.DateAdded.CompareTo(b.DateAdded));
                return list;
            }
        }

        public void AddVideoNotifications(IEnumerable<KeyValuePair<DualSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>> notifications)
        {
            lock (this)
            {
                foreach (var info in notifications)
                {
                    var id = Guid.NewGuid().ToString();
                    ytVideoList.Add(id, info);
                    Log.Debug("Video url1: " + info.Key.Uri1);
                    Log.Debug("Video url2: " + info.Key.Uri2);
                    this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                    {
                        MediaInfo = new MediaInfo(id, info.Key.File, info.Value.DescriptionText,
                        DateTime.Now, info.Value.TabId)
                    });
                }
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotifications(IEnumerable<KeyValuePair<SingleSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>> notifications)
        {
            lock (this)
            {
                foreach (var info in notifications)
                {
                    var id = Guid.NewGuid().ToString();
                    videoList.Add(id, info);
                    Log.Debug("Video url1: " + info.Key.Uri);
                    this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                    {
                        MediaInfo = new MediaInfo(id, info.Key.File, info.Value.DescriptionText,
                        DateTime.Now, info.Value.TabId)
                    });
                }
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotifications(IEnumerable<KeyValuePair<MultiSourceHLSDownloadInfo, StreamingVideoDisplayInfo>> notifications)
        {
            lock (this)
            {
                foreach (var info in notifications)
                {
                    var id = Guid.NewGuid().ToString();
                    hlsVideoList.Add(id, info);
                    Log.Debug("Video url1: " + info.Key.VideoUri);
                    Log.Debug("Video url2: " + info.Key.AudioUri);
                    this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                    {
                        MediaInfo = new MediaInfo(id, info.Key.File, info.Value.DescriptionText,
                        DateTime.Now, info.Value.TabId)
                    });
                }
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotifications(IEnumerable<KeyValuePair<MultiSourceDASHDownloadInfo, StreamingVideoDisplayInfo>> notifications)
        {
            lock (this)
            {
                foreach (var info in notifications)
                {
                    var id = Guid.NewGuid().ToString();
                    dashVideoList.Add(id, info);
                    Log.Debug("Video url1: " + info.Key.Url);
                    this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                    {
                        MediaInfo = new MediaInfo(id, info.Key.File, info.Value.DescriptionText,
                        DateTime.Now, info.Value.TabId)
                    });
                }
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotification(StreamingVideoDisplayInfo displayInfo, DualSourceHTTPDownloadInfo info)
        {
            lock (this)
            {
                var id = Guid.NewGuid().ToString();
                ytVideoList.Add(id, new KeyValuePair<DualSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>(info, displayInfo));
                Log.Debug("Video url1: " + info.Uri1);
                Log.Debug("Video url2: " + info.Uri2);
                this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                {
                    MediaInfo = new MediaInfo(id, info.File, displayInfo.DescriptionText,
                    DateTime.Now, displayInfo.TabId)
                });
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotification(StreamingVideoDisplayInfo displayInfo, SingleSourceHTTPDownloadInfo info)
        {
            lock (this)
            {
                var id = Guid.NewGuid().ToString();
                videoList.Add(id, new KeyValuePair<SingleSourceHTTPDownloadInfo, StreamingVideoDisplayInfo>(info, displayInfo));
                Log.Debug("Video url1: " + info.Uri);
                this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                {
                    MediaInfo = new MediaInfo(id, info.File, displayInfo.DescriptionText,
                    DateTime.Now, displayInfo.TabId)
                });
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotification(StreamingVideoDisplayInfo displayInfo, MultiSourceHLSDownloadInfo info)
        {
            lock (this)
            {
                var id = Guid.NewGuid().ToString();
                hlsVideoList.Add(id, new KeyValuePair<MultiSourceHLSDownloadInfo, StreamingVideoDisplayInfo>(info, displayInfo));
                Log.Debug("Video url1: " + info.VideoUri);
                Log.Debug("Video url2: " + info.AudioUri);
                this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                {
                    MediaInfo = new MediaInfo(id, info.File, displayInfo.DescriptionText,
                    DateTime.Now, displayInfo.TabId)
                });
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoNotification(StreamingVideoDisplayInfo displayInfo, MultiSourceDASHDownloadInfo info)
        {
            lock (this)
            {
                var id = Guid.NewGuid().ToString();
                Log.Debug("DASH video added with id: " + id);
                dashVideoList.Add(id, new KeyValuePair<MultiSourceDASHDownloadInfo, StreamingVideoDisplayInfo>(info, displayInfo));
                Log.Debug("Video url1: " + info.Url);
                this.MediaAdded?.Invoke(this, new MediaInfoEventArgs
                {
                    MediaInfo = new MediaInfo(id, info.File, displayInfo.DescriptionText,
                    DateTime.Now, displayInfo.TabId)
                });
                ApplicationContext.PlatformUIService.ShowMediaNotification();
                ApplicationContext.BroadcastConfigChange();
            }
        }

        public void AddVideoDownload(string videoId)
        {
            Log.Debug($"[VID] Received video download request for ID: {videoId}");
            Log.Debug($"[VID] ytVideoList count: {ytVideoList.Count}, videoList count: {videoList.Count}, hlsVideoList count: {hlsVideoList.Count}, dashVideoList count: {dashVideoList.Count}");

            var name = string.Empty;
            var size = 0L;
            var contentType = string.Empty;
            var valid = false;
            if (ytVideoList.ContainsKey(videoId))
            {
                if (ApplicationContext.LinkRefresher.LinkAccepted(ytVideoList[videoId].Key)) return;
                name = ytVideoList[videoId].Key.File;
                size = ytVideoList[videoId].Value.Size;
                contentType = ytVideoList[videoId].Key.ContentType1;
                valid = true;
            }
            else if (videoList.ContainsKey(videoId))
            {
                Log.Debug($"[VID] Found in videoList, checking LinkRefresher...");
                var videoData = videoList[videoId].Key;
                var uri = videoData.Uri;
                var cookies = videoData.Cookies;
                var headers = videoData.Headers;

                Log.Debug($"[VID-DATA] URI: {uri}");
                Log.Debug($"[VID-DATA] File: {videoData.File}");
                Log.Debug($"[VID-DATA] Size: {videoList[videoId].Value.Size} bytes");
                Log.Debug($"[VID-DATA] Content-Type: {videoData.ContentType}");
                Log.Debug($"[VID-DATA] Cookies: {(string.IsNullOrEmpty(cookies) ? "NONE" : cookies?.Substring(0, Math.Min(100, cookies.Length)))}");
                Log.Debug($"[VID-DATA] Headers count: {headers?.Count ?? 0}");

                if (ApplicationContext.LinkRefresher.LinkAccepted(videoData))
                {
                    Log.Debug($"[VID] LinkRefresher.LinkAccepted returned TRUE - dialog already shown, returning");
                    return;
                }

                Log.Debug($"[VID] LinkRefresher.LinkAccepted returned FALSE - will show dialog");
                name = videoData.File;
                size = videoList[videoId].Value.Size;
                contentType = videoData.ContentType;
                valid = true;
            }
            else if (hlsVideoList.ContainsKey(videoId))
            {
                Log.Debug("Download HLS video added with id: " + videoId);
                name = hlsVideoList[videoId].Key.File;
                valid = true;
                try
                {
                    contentType = hlsVideoList[videoId].Key.ContentType;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, ex.Message);
                }
            }
            else if (dashVideoList.ContainsKey(videoId))
            {
                Log.Debug("Download DASH video added with id: " + videoId);
                name = dashVideoList[videoId].Key.File;
                contentType = dashVideoList[videoId].Key.ContentType;
                valid = true;
            }
            if (valid)
            {
                Log.Debug($"[VID] Video found! Name: {name}, Size: {size}, Type: {contentType}");
                if (Config.Instance.StartDownloadAutomatically && IsFFmpegOK(videoId))
                {
                    Log.Debug("[VID] Starting download automatically");
                    StartVideoDownload(
                        videoId, FileHelper.SanitizeFileName(name),
                        null, true, null, Config.Instance.Proxy,
                    Helpers.GetSpeedLimit(), null);
                }
                else
                {
                    Log.Debug("[VID] Showing video download dialog");
                    ApplicationContext.Application.ShowVideoDownloadDialog(videoId, name, size, contentType);
                }
            }
            else
            {
                Log.Debug($"[VID] ERROR: Video ID '{videoId}' not found in any video list!");
                Log.Debug($"[VID] Total videos in memory: yt={ytVideoList.Count}, video={videoList.Count}, hls={hlsVideoList.Count}, dash={dashVideoList.Count}");
            }
        }

        public static bool IsFFmpegOK(string id)
        {
            if (!ApplicationContext.VideoTracker.IsFFmpegRequiredForDownload(id)) return true;
            return FFmpegMediaProcessor.IsFFmpegInstalled();
        }
    }

    public class MediaInfo
    {
        public MediaInfo(string id, string name, string description,
            DateTime date, string tabId)
        {
            this.ID = id;
            this.Name = name;
            this.Description = description;
            this.DateAdded = date;
            this.TabId = tabId;
        }

        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateAdded { get; set; }
        public string TabId { get; set; }
    }

    public class MediaInfoEventArgs :EventArgs
    {
        public MediaInfo MediaInfo { get; set; }
    }
}
