﻿using Mendix.StudioPro.ExtensionsAPI.Model;
using Mendix.StudioPro.ExtensionsAPI.Model.Projects;
using Mendix.StudioPro.ExtensionsAPI.Services;
using Mendix.StudioPro.ExtensionsAPI.UI.Services;
using System.Net;
using System.Text;

namespace LowCodeConnect.OpenPageFromBrowser
{

    public sealed class BackgroundHttpServer
    {
        private static readonly Lazy<BackgroundHttpServer> _instance =
            new(() => new BackgroundHttpServer());

        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;
        private bool _isRunning;
        private IModel? _currentApp;
        private IDockingWindowService? _dockingWindowsService;
        private ILogService? _logService;

        private BackgroundHttpServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:8387/");
            _cts = new CancellationTokenSource();
        }

        ~BackgroundHttpServer() {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
        }

        public static BackgroundHttpServer Instance => _instance.Value;

        public void SetCurrentApp(IModel? currentApp)
        {
            _currentApp = currentApp;
        }

        public void Start(IModel? currentApp, IDockingWindowService dockingWindowService, ILogService logService, IMessageBoxService messageBoxService)
        {
            _logService = logService;
            _currentApp = currentApp;
            _dockingWindowsService = dockingWindowService;
            messageBoxService.ShowInformation("Extension is installed. Create a shortcut in your browser with this javascript (see copy details)",
                "javascript:(function(){ fetch(\"http://localhost:8387/\" + mx.ui.getContentForm().path);})();");

            if (!_isRunning)
            {
                _isRunning = true;
                Task.Run(async () =>
                {
                    _listener.Start();
                    _logService?.Info("Server is running...");

                    while (!_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            HttpListenerContext context = await _listener.GetContextAsync();
                            HandleRequest(context);
                        }
                        catch (HttpListenerException e) when (_cts.Token.IsCancellationRequested)
                        {
                            _logService?.Info($"Canceled: {e.Message}");
                            _isRunning = false;
                        }
                        catch (Exception ex)
                        {
                            _logService?.Info($"Error: {ex.Message}");
                        }
                    }
                    _listener.Stop();
                    _logService?.Info($"Canceled");
                    _isRunning = false;
                }, _cts.Token);

            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            _isRunning = false;

            _logService?.Info("Server has stopped.");
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            if (request != null && request.Url != null && request.Url.AbsolutePath != null)
            {
                // remove the first '/'
                OpenDocumentEditor(request.Url.AbsolutePath.ToString()[1..]);
                HttpListenerResponse response = context.Response;
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                response.StatusCode = (int)HttpStatusCode.OK;
                byte[] buffer = [];

                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        private void OpenDocumentEditor(string docQualName)
        {
            if (docQualName != null && this._currentApp != null && _dockingWindowsService != null && docQualName != null && docQualName.Length >= 3 && docQualName.Contains('.'))
            {
                string[] docSplit = docQualName.Split('/');
                if (docSplit.Length == 2)
                {
                    string moduleName = docSplit[0];
                    string? docName = docSplit[1]?.Replace(".page.xml", "");
                    if (docName != null && GetModuleFromDocument(moduleName) is IModule module)
                    {
                        IDocument? docExists = GetDocumentFromModule(module, docName);
                        if (docExists != null)
                        {
                            _dockingWindowsService.TryOpenEditor(docExists);
                        }
                    }
                }
                else
                {
                    _logService?.Info("Invalid request received " + docQualName);
                }
            }
        }

        public static IDocument? GetDocumentFromModule(IModule module, string documentName)
        {
            IDocument? document = module.GetDocuments()
                .Where(doc => doc.Name == documentName)
                .FirstOrDefault();

            if (document != null)
            {
                return document;
            }

            foreach (var folder in module.GetFolders())
            {
                IDocument? returnDoc = GetDocumentFromFolder(folder, documentName);
                if (returnDoc != null)
                {
                    return returnDoc;
                }
            }
            return null;
        }
        public IModule? GetModuleFromDocument(string docQualName)
        {
            if (_currentApp == null)
                return null;

            var moduleName = docQualName.Split('.')[0];
            var module = _currentApp.Root.GetModules()
                .FirstOrDefault(m => m.Name == moduleName);

            return module;
        }

        public static IDocument? GetDocumentFromFolder(IFolder folder, string documentName)
        {
            return folder.GetDocuments()
                         .FirstOrDefault(doc => doc.Name == documentName);
        }
    }
}
