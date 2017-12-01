using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace UmbracoMediaCopy.Core.Controllers
{
    [PluginController("Website")]
    public class MediaExtensionsApiController : UmbracoAuthorizedApiController
    {

        private string _lastCopiedNodePath;
        private const string IMAGE_CONTENT_TYPE_ALIAS = "Image";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentId">The node id that we want to copy the "nodeId" to.</param>
        /// <param name="nodeId">The node to copy.</param>
        /// <param name="copyChildren">Should we coupy the children?</param>
        /// <returns></returns>
        [HttpPost]
        public object Copy(int parentId, int nodeId, bool copyChildren)
        {
            //Get the media that we want to copy. 
            IMedia mediaToCopy = Services.MediaService.GetById(nodeId);

            if (mediaToCopy == null)
            {
                return new { success = false, message = "Could not find the media item to copy." };
            }

            IMedia mediaDestination = Services.MediaService.GetById(parentId);

            if (mediaDestination == null && parentId != -1)//-1 indicates that we are putting it under the root.
            {
                return new { success = false, message = "Could not find the media item to copy to." };
            }

            //If we are trying to copy to the root and the content type is not allowed at the root or if we are trying to copy it under a content type which its not allowed.
            if ((parentId == -1 && !mediaToCopy.ContentType.AllowedAsRoot) || (parentId != -1 && !IsContentTypeAllowedUnderContentType(mediaDestination.ContentType, mediaToCopy.ContentType)))
            {
                string mediaToCopyName = mediaToCopy.Name;
                string destinationName = parentId == -1 ? "Media" : mediaDestination.Name;
                return new { success = false, message = "You are not allowed to copy " + mediaToCopyName + " under " + destinationName + "." };
            }

            //IMedia copiedMedia = Services.MediaService.CreateMediaWithIdentity(mediaToCopy.Name + " Copy", mediaDestination.Id, mediaToCopy.ContentType.Alias);

            bool allSuccessful = CopyMedia(mediaToCopy, parentId, copyChildren);

            if (!allSuccessful)
            {
                return new { success = false, message = "We encountered a problem with the media during copy. Please check the error log for details." };
            }
            else
            {
                return new { success = true, path = this._lastCopiedNodePath };
            }

        }


        private bool IsContentTypeAllowedUnderContentType(IMediaType targetContent, IMediaType contentToTest)
        {
            ContentTypeSort cts = targetContent.AllowedContentTypes.Where(x => x.Id.Value == contentToTest.Id).FirstOrDefault();

            return cts != null;
        }


        private bool CopyMedia(IMedia mediaToCopy, int destinationId, bool copyChildren)
        {
            //Copy the first media then we will determine if/how to copy the children.
            IMedia freshlyCopiedMedia = CopyMediaPropertiesAndSave(mediaToCopy, destinationId);

            if (freshlyCopiedMedia == null)
            {
                return false;
            }

            this._lastCopiedNodePath = freshlyCopiedMedia.Path;

            if (copyChildren)
            {
                foreach (IMedia childMediaToCopy in mediaToCopy.Children())
                {
                    bool allSuccessfull = CopyMedia(childMediaToCopy, freshlyCopiedMedia.Id, copyChildren);

                    if (!allSuccessfull)
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        private IMedia CopyMediaPropertiesAndSave(IMedia mediaToCopy, int destinationId)
        {
            try
            {
                IMedia copiedMedia = Services.MediaService.CreateMediaWithIdentity(mediaToCopy.Name, destinationId, mediaToCopy.ContentType.Alias);

                bool allSuccessful = CopyProperties(copiedMedia, mediaToCopy);

                if (!allSuccessful)
                {
                    return null;
                }

                Services.MediaService.Save(copiedMedia);

                return copiedMedia;
            }
            catch (Exception e)
            {
                throw new Exception("Could not copy a IMedia item", e);
            }
        }

        private bool CopyProperties(IMedia copiedMedia, IMedia mediaToCopy)
        {
            for (int i = 0; i < mediaToCopy.Properties.Count; ++i)
            {
                Property toCopyProperty = mediaToCopy.Properties[i];
                Property copiedProperty = copiedMedia.Properties[i];
                if (toCopyProperty.Alias == "umbracoFile")
                {
                    Property cp = CopyUmbracoFileProperty(toCopyProperty);

                    if (cp == null)
                    {
                        return false;
                    }
                    else
                    {
                        copiedProperty.Value = cp.Value;
                    }
                }
                else
                {
                    copiedProperty.Value = toCopyProperty.Value;
                }
            }
            return true;
        }


        private Property CopyUmbracoFileProperty(Property umbFileProp)
        {
            if (umbFileProp.Value == null)
            {
                return umbFileProp;
            }
            string imageSource;
            dynamic umbFile = new { };
            bool isPropertyDynamic = false;
            try
            {
                umbFile = JsonConvert.DeserializeObject<dynamic>(umbFileProp.Value.ToString());
                imageSource = umbFile.src;
                isPropertyDynamic = true;
            }
            catch (Exception e)
            {
                imageSource = umbFileProp.Value.ToString();
            }


            if (String.IsNullOrEmpty(imageSource))
            {
                return umbFileProp;
            }

            string currentFolderName = GetCurrentFolderName(imageSource);

            string src = imageSource.Replace("/media", "");

            if (!VirtualPathUtility.IsAbsolute(src))
            {
                Uri uriImageSource = new Uri(src);
                src = uriImageSource.PathAndQuery;
            }

            try
            {
                //Get the media folder number.
                long newFolderName = GetMediaFolderCount();

                //convert the old source into the new image source.
                string newImageSrc = "/media" + src.Replace(currentFolderName, newFolderName.ToString());

                //Get the index of the last /, which should be after the media folder number
                int lastIndexOfSlash = newImageSrc.LastIndexOf("/");

                //Remove the image name from the path.
                string newImageFolder = newImageSrc.Substring(0, lastIndexOfSlash);

                //Map the new media folder path to the server.
                string mappedServerPath = HttpContext.Current.Server.MapPath(newImageFolder);
                string mappedNewImageSrc = HttpContext.Current.Server.MapPath(newImageSrc);

                //Create the directory for the new media folder
                DirectoryInfo di = Directory.CreateDirectory(mappedServerPath);

                //Get umbracos MediaFileSystem
                MediaFileSystem fs = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();

                //Copy the old image to the new directory.
                try
                {
                    //Most likely reason to fail would be that the src file doesn't exist on the filesystem
                    fs.CopyFile(src, mappedNewImageSrc);
                }
                catch (Exception e)
                {
                    //If this failed just take the imageSource and use it as the property value, no use making everything crap out.
                    return umbFileProp; //Return this because no changes/files were made/created. 
                }

                //Now set the umbfilesource to the new source.
                string fullNewImageSource = imageSource.Replace(currentFolderName, newFolderName.ToString());

                if (!VirtualPathUtility.IsAbsolute(imageSource))
                {
                    //So we are dealing with a full url "https://something.com/bla/blah/blah.png
                    Uri fullUri = new Uri(imageSource);
                    fullNewImageSource = fullUri.GetLeftPart(UriPartial.Authority) + fullNewImageSource;
                }

                if (isPropertyDynamic)
                {
                    umbFile.src = fullNewImageSource;
                    umbFileProp.Value = JsonConvert.SerializeObject(umbFile);
                }
                else
                {
                    //its just a string so we can just set the value
                    umbFileProp.Value = fullNewImageSource;
                }


                return umbFileProp;

            }
            catch (Exception e)
            {
                return null;
            }
        }


        private string GetCurrentFolderName(string imageSource)
        {
            MatchCollection matches = Regex.Matches(imageSource, @"(?:media)\/(\d+)");

            string currentFolderName = "";
            if (matches != null && matches.Count > 0)
            {
                IEnumerator matchesEnumerator = matches.GetEnumerator();
                matchesEnumerator.MoveNext();
                Match firstMatch = (Match)matchesEnumerator.Current;

                GroupCollection gc = firstMatch.Groups;

                for (int i = 0; i < gc.Count; ++i)
                {
                    if (i == 1)
                    {
                        Group g = gc[1];
                        currentFolderName = g.Value;
                        break;
                    }
                }
            }

            return currentFolderName.ToString();
        }


        private long GetMediaFolderCount()
        {
            long _numberedFolder = 1000;
            var folders = new List<long>();
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();
            var directories = fs.GetDirectories("");
            foreach (var directory in directories)
            {
                long dirNum;
                if (long.TryParse(directory, out dirNum))
                {
                    folders.Add(dirNum);
                }
            }
            var last = folders.OrderBy(x => x).LastOrDefault();
            if (last != default(long))
                _numberedFolder = last;

            return _numberedFolder + 1;
        }

        public class UmbracoFile
        {
            public string src { get; set; }
            public IEnumerable<Crop> crops { get; set; }
            public FocalPoint focalPoint { get; set; }
        }

        public class FocalPoint
        {
            public double top { get; set; }
            public double left { get; set; }
        }

        public class Crop
        {
            public int height { get; set; }
            public int width { get; set; }
            public string alias { get; set; }
            public Coordinates coordinates { get; set; }
        }

        public class Coordinates
        {
            public float x1 { get; set; }
            public float y1 { get; set; }
            public float x2 { get; set; }
            public float y2 { get; set; }
        }
    }
}
