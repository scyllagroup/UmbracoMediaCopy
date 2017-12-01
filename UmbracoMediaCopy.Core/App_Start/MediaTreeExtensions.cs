using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Trees;

namespace UmbracoMediaCopy.Core.App_Start
{
    public class MediaTreeExtensions : ApplicationEventHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MediaTreeExtensions() : base()
        {
            TreeControllerBase.MenuRendering += AddCopyToMediaMenu;
        }
        
        private void AddCopyToMediaMenu(TreeControllerBase sender, MenuRenderingEventArgs args)
        {
            if (sender.TreeAlias != "media")
            {
                //We aren't dealing with a media tree so we don't need to do anything
                return;
            }

            MenuItem menuItem = new MenuItem("copy", "Copy");

            menuItem.Icon = "documents";
            //menuItem.SeperatorBefore = true;
            //menuItem.LaunchDialogUrl("")

            menuItem.LaunchDialogView("/App_Plugins/MediaTreeExtensions/views/CopyMediaDialog.html", "Copy");

            args.Menu.Items.Insert(args.Menu.Items.Count - 1, menuItem);
        }
    }
}
