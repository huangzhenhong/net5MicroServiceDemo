using Consul;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Web.Models;
using Web.Utility;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private static int iSeed = 0; // 没考虑溢出问题
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            #region 通过consul去发现这些服务地址
            {
                //using (ConsulClient client = new ConsulClient(c =>
                //{
                //    c.Address = new Uri("http://localhost:8500/");
                //    c.Datacenter = "dc1";
                //}))
                //{
                //    var dictionary = client.Agent.Services().Result.Response;
                //    string message = "";
                //    foreach (var keyValuePair in dictionary)
                //    {
                //        AgentService agentService = keyValuePair.Value;
                //        this._logger.LogWarning($"{agentService.Address}:{agentService.Port} {agentService.ID} {agentService.Service}");//找的是全部服务 全部实例  其实可以通过ServiceName筛选
                //        message += $"{agentService.Address}:{agentService.Port};";
                //    }
                //    //获取当前consul的全部服务
                //    base.ViewBag.Message = message;
                //}
            }
            #endregion

            #region 调用---负载均衡
            {
                //string url = "http://localhost:5726/WeatherForecast";
                //string url = "http://localhost:5727/WeatherForecast";
                //string url = "http://localhost:5728/WeatherForecast";
                string url = "http://MyServices/WeatherForecast";
                //consul解决使用服务名字 转换IP:Port----DNS

                Uri uri = new Uri(url);
                string groupName = uri.Host;
                string message = "";
                using (ConsulClient client = new ConsulClient(c =>
                {
                    c.Address = new Uri("http://localhost:8500/");  
                    c.Datacenter = "dc1";
                }))
                {
                    var dictionary = client.Agent.Services().Result.Response;
                    var list = dictionary.Where(k => k.Value.Service.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                    KeyValuePair<string, AgentService> keyValuePair = new KeyValuePair<string, AgentService>();
                    //foreach (var cc in list) {
                    //    AgentService agentService = cc.Value;
                    //    message += $"{agentService.Address}:{agentService.Port};";
                    //}  

                    
                    // 随机
                    //var array = list.ToArray();
                    //keyValuePair = array[new Random(iSeed++).Next(0, array.Length)];
                    
                    // 轮询
                    //var array = list.ToArray();
                    //keyValuePair = array[iSeed++ % array.Length];
                    
                    // 权重
                    List<KeyValuePair<string, AgentService>> serviceList = new List<KeyValuePair<string, AgentService>>();

                    foreach (var pair in list) {
                        int count = int.Parse(pair.Value.Tags?[0]);
                        for (int i = 0; i < count; i++) {
                            serviceList.Add(pair);
                        }
                    }
                    keyValuePair = serviceList.ToArray()[new Random(iSeed++).Next(0, serviceList.Count())];
                    

                    string hostNew = $"{keyValuePair.Value.Address}:{keyValuePair.Value.Port}";
                    url = url.Replace(groupName, hostNew, StringComparison.OrdinalIgnoreCase);
                    //string result = WebApiHelperExtend.InvokeApi(url);  

                    base.ViewBag.Message = url;
                }
            }
            #endregion

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private void getService() { 
            
        }
    }
}
