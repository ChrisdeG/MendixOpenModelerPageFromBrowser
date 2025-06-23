using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.Events;
using Mendix.StudioPro.ExtensionsAPI.UI.Menu;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;

namespace LowCodeConnect.OpenPageFromBrowser
{
    [Export(typeof(MenuExtension))]
    public class MyMenuExtension: MenuExtension
    {

        //Server? server;
        private readonly IDockingWindowService dockingWindowService;
        private readonly ILogService logService;
        private readonly IMessageBoxService messageBoxService;

        [ImportingConstructor]
        public MyMenuExtension(IDockingWindowService dockingWindowService, ILogService logService, IMessageBoxService messageBoxService)
        {
            Subscribe<ExtensionLoaded>(OnExtensionLoaded);
            Subscribe<ExtensionUnloading>(OnExtensionUnloading);
            this.dockingWindowService = dockingWindowService;
            this.logService = logService;
            this.messageBoxService = messageBoxService;
        }

        void OnExtensionLoaded()
        {
            logService.Info("Show page from browser extension loaded");
        }

        void OnExtensionUnloading()
        {
            logService.Info("Show page from browser extension unloading");
            BackgroundHttpServer.Instance.Stop();
        }
        public override IEnumerable<MenuViewModel> GetMenus()
        {
            yield return new MenuViewModel("Open page from browser", () => BackgroundHttpServer.Instance.Start(
                CurrentApp, dockingWindowService, logService, messageBoxService) );
        }
        
        void StartServer()
        {
            if (CurrentApp != null)
            {
                BackgroundHttpServer.Instance.setCurrentApp(CurrentApp);
            }
        }    
    }

}



