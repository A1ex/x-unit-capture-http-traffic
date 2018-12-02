using System;
using Xunit;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Proxy = Titanium.Web.Proxy.Http;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Web;

namespace x_unit_capture_http_traffic_1
{

    public class UnitTest1: IDisposable
    {

        public IWebDriver _driver;
        public ProxyServer _proxyServer;
        public readonly IDictionary<int, Proxy.Request> _requestsHistory =
            new ConcurrentDictionary<int, Proxy.Request>();
        public readonly IDictionary<int, Proxy.Response> _responsesHistory =
            new ConcurrentDictionary<int, Proxy.Response>();

        public Dictionary<string, string> postParamters =  new Dictionary<string, string>();

        public UnitTest1()
        {

            _proxyServer = new ProxyServer();
            var explicitEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 18917, true);
            _proxyServer.AddEndPoint(explicitEndPoint);
            _proxyServer.Start();
            _proxyServer.SetAsSystemHttpProxy(explicitEndPoint);
            _proxyServer.SetAsSystemHttpsProxy(explicitEndPoint);
            //_proxyServer.BeforeRequest += OnRequestModifyTrafficEventHandler;
            _proxyServer.BeforeRequest += OnRequestCaptureTrafficEventHandler;
            //_proxyServer.BeforeResponse += OnResponseModifyTrafficEventHandler;
            //_proxyServer.BeforeResponse += OnResponseCaptureTrafficEventHandler;

            var proxy = new OpenQA.Selenium.Proxy
            {
                HttpProxy = "http://localhost:18917",
                SslProxy = "http://localhost:18917",
                FtpProxy = "http://localhost:18917"
            };
            var options = new ChromeOptions
            {
                Proxy = proxy,
            };
            _driver = new ChromeDriver(options);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(180);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(180);
            _driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(180);
        }

        public void Dispose()
        {
            _proxyServer.Stop();
            _driver.Dispose();
            _requestsHistory.Clear();
            _responsesHistory.Clear();
        }

        public async Task OnRequestCaptureTrafficEventHandler(object sender, SessionEventArgs e) => await Task.Run(
            () =>
            {
                var method = e.WebSession.Request.Method.ToUpper();
                if (method == "POST"
                    && e.WebSession.Request.Url.IndexOf("25b6acb2-de68-11e2-a28f-0800200c9a66") != -1)
                {
                    //Get/Set request body bytes
                    byte[] bodyBytes = e.GetRequestBody().Result;
                    e.SetRequestBody(bodyBytes);

                    //Get/Set request body as string
                    string bodyString = e.GetRequestBodyAsString().Result;
                    e.SetRequestBodyString(bodyString);
                    _requestsHistory.Add(e.WebSession.Request.GetHashCode(), e.WebSession.Request);
                    var dict = HttpUtility.ParseQueryString(bodyString);
                    postParamters = dict.AllKeys.ToDictionary
                                                        (
                                                            key => key,
                                                            key => dict[key]
                                                        );
                }
            });

        public void OnRequestBlockResourceEventHandler(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.ToString().Contains("analytics"))
            {
                string customBody = string.Empty;
                e.Ok(Encoding.UTF8.GetBytes(customBody));
            }
        }

        public void OnRequestRedirectTrafficEventHandler(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.RequestUri.AbsoluteUri.Contains("logo.svg"))
            {
                e.Redirect("https://automatetheplanet.com/wp-content/uploads/2016/12/homepage-img-1.svg");
            }
        }

        public async Task OnRequestModifyTrafficEventHandler(object sender, SessionEventArgs e) => await Task.Run(
            () =>
            {
                var method = e.WebSession.Request.Method.ToUpper();
                if ((method == "POST" || method == "PUT" || method == "PATCH" || method == "GET"))
                {
                    //Get/Set request body bytes
                    if (e.WebSession.Request.ContentLength != -1)
                    {
                        byte[] bodyBytes = e.GetRequestBody().Result;
                        e.SetRequestBody(bodyBytes);

                        //Get/Set request body as string
                        string bodyString = e.GetRequestBodyAsString().Result;
                        e.SetRequestBodyString(bodyString);
                    }
                }
            });

        public async Task OnResponseCaptureTrafficEventHandler(object sender, SessionEventArgs e) => await Task.Run(
            () =>
            {
                if (!_responsesHistory.ContainsKey(e.WebSession.Response.GetHashCode()) && e.WebSession != null && e.WebSession.Response != null)
                {
                    _responsesHistory.Add(e.WebSession.Response.GetHashCode(), e.WebSession.Response);
                }
            });

        public void OnResponseModifyTrafficEventHandler(object sender, SessionEventArgs e)
        {
            if (e.WebSession.Request.Method == "GET" || e.WebSession.Request.Method == "POST")
            {
                if (e.WebSession.Response.StatusCode == 200)
                {
                    if (e.WebSession.Response.ContentType != null && e.WebSession.Response.ContentType.Trim().ToLower().Contains("text/html"))
                    {
                        byte[] bodyBytes = e.GetResponseBody().Result;
                        e.SetResponseBody(bodyBytes);

                        string body = e.GetResponseBodyAsString().Result;
                        e.SetResponseBodyString(body);
                    }
                }
            }
        }


        [Fact]
        public Boolean CheckRelevantIDCalled()
        {
            postParamters.Clear();
            _driver.Navigate().GoToUrl("https://surveys.ipsosinteractive.com/surveystest/?pid=S1050000&id=123&ci=fr-fr");
            //Dispose();
            return postParamters["RelevantIdCalled"].Equals("no");      
        }

    }


}
