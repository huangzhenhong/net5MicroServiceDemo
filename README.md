# net5MicroServiceDemo

# dotnet5 + Consul + Ocelet 网关

## 项目准备
- 新建3个dotnet5的项目
    - WebApi
    - WebApp (dotnetcore Web Application MVC)
    - Gateway (也是一个WebApi的项目类型)
## 下载Consul
consul_1.9.2_windows_amd64

下载完成之后，使用cmd来运行
```
consul agent -dev
```
打开 http://localhost:8500

## 配置Consul
#### Consul是在WebApi里面配置的
1. 安装Consul包
2. 新建一个ConsulHelper.cs 类
3. 修改StartUp.cs文件
4. 测试

##### 安装Consul包
直接使用NuGet安装Consule包，Consul.NET
##### 新建一个ConsulHelper.cs 类
这里定义了IConfiguration的一个扩展方法
```
using Consul;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApiDemo.Utility
{
    public static class ConsulHelper
    {
        public static void ConsulRegist(this IConfiguration configuration)
        {
            ConsulClient client = new ConsulClient(c =>
            {
                c.Address = new Uri("http://localhost:8500/");
                c.Datacenter = "dc1";
            });
            string ip = configuration["ip"];
            int port = int.Parse(configuration["port"]);// command line inputs
            int weight = string.IsNullOrWhiteSpace(configuration["weight"]) ? 1 : int.Parse(configuration["weight"]);// weight for load balance
            client.Agent.ServiceRegister(new AgentServiceRegistration()
            {
                ID = "service" + ip + ":" + port,// unique Id
                Name = "MyConsulServices",//service group name
                Address = ip,//ip address
                Port = port, 
                Tags = new string[] { weight.ToString() },//tags
                Check = new AgentServiceCheck()// health check
                {
                    Interval = TimeSpan.FromSeconds(12),
                    HTTP = $"http://{ip}:{port}/api/Health/Index",
                    Timeout = TimeSpan.FromSeconds(5),
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5)
                }
            });
            Console.WriteLine($"http://{ip}:{port} finished register");
        }
    }
}
```
##### 修改StartUp.cs文件
```
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApiDemo.Utility;

namespace WebApiDemo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WebApiDemo", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApiDemo v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // dotnet WebApiDemo.dll --urls="http://*:5726" --ip="127.0.0.1" --port=5726 --weight=1
            // dotnet WebApiDemo.dll --urls="http://*:5727" --ip="127.0.0.1" --port=5727 --weight=3
            // dotnet WebApiDemo.dll --urls="http://*:5728" --ip="127.0.0.1" --port=5728 --weight=10

            // start consul regist
            this.Configuration.ConsulRegist();
        }
    }
}

```
##### 测试consul是否工作

启动3个实例
```
// dotnet WebApiDemo.dll --urls="http://*:5726" --ip="127.0.0.1" --port=5726 --weight=1
// dotnet WebApiDemo.dll --urls="http://*:5727" --ip="127.0.0.1" --port=5727 --weight=3
// dotnet WebApiDemo.dll --urls="http://*:5728" --ip="127.0.0.1" --port=5728 --weight=10
```
回到浏览器localhost:8500应该就可以看到一个名称问MyConsulServices的服务启动了，并且在它下面有3个Instance在运行

##### 新建一个Web Application用于测试consul 
- 安装Consul包
- 修改HomeController.cs文件
- 测试

###### HomeController.cs
```
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
                string url = "http://MyConsulServices/WeatherForecast";
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

```
这是WebApiHelperExtend.cs的代码
```
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Web.Utility
{
    public static class WebApiHelperExtend
    {
        public static string InvokeApi(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                HttpRequestMessage message = new HttpRequestMessage();
                message.Method = HttpMethod.Get;
                message.RequestUri = new Uri(url);
                var result = httpClient.SendAsync(message).Result;
                string content = result.Content.ReadAsStringAsync().Result;
                return content;
            }
        }
    }
}

```

测试： 可以直接启动项目，因为HomeController会直接调用它的index方法，从而访问consul服务。可以看到最终效果是http://MyConsulServices/WeatherForecast 可以实现请求转发的功能。

#### 配置Ocelot网关
##### 新建一个MicroServiceGateWay的WebApi项目
- 安装Ocelot包
- 安装Ocelot.Provider.Consul包
- 安装Ocelot.Provider.Polly包

##### 新建一个ocelot.config.json文件
```
////****************************单地址模式********************************
//{
//  "Routes": [
//    {
//      "DownstreamPathTemplate": "/api/{url}", //服务地址--url变量
//      "DownstreamScheme": "http",
//      "DownstreamHostAndPorts": [
//        {
//          "Host": "localhost",
//          "Port": 5726 // 服务端口
//        } //可以多个，自行负载均衡
//      ],
//      "UpstreamPathTemplate": "/T5726/{url}", //网关地址--url变量 //冲突的还可以加权值  
//      "UpstreamHttpMethod": [ "Get", "Post" ]
//    }
//  ]
//}

////*****************************多地址实例********************************
//{
//  "Routes": [
//    {
//      "DownstreamPathTemplate": "/api/{url}",
//      "DownstreamScheme": "http",
//      "DownstreamHostAndPorts": [
//        {
//          "Host": "localhost",
//          "Port": 5726
//        }
//      ],
//      "UpstreamPathTemplate": "/T5726/{url}",
//      "UpstreamHttpMethod": [ "Get", "Post" ]
//    },
//    {
//      "DownstreamPathTemplate": "/api/{url}",
//      "DownstreamScheme": "http",
//      "DownstreamHostAndPorts": [
//        {
//          "Host": "localhost",
//          "Port": 5727
//        }
//      ],
//      "UpstreamPathTemplate": "/T5727/{url}",
//      "UpstreamHttpMethod": [ "Get", "Post" ]
//    },
//    {
//      "DownstreamPathTemplate": "/api/{url}",
//      "DownstreamScheme": "http",
//      "DownstreamHostAndPorts": [
//        {
//          "Host": "localhost",
//          "Port": 5728
//        }
//      ],
//      "UpstreamPathTemplate": "/T5728/{url}",
//      "UpstreamHttpMethod": [ "Get", "Post" ]
//    }
//  ]
//}

////*****************************负载均衡********************************
//{
//  "Routes": [
//    {
//      "DownstreamPathTemplate": "/api/{url}",
//      "DownstreamScheme": "http",
//      "DownstreamHostAndPorts": [
//        {
//          "Host": "localhost",
//          "Port": 5726
//        },
//        {
//          "Host": "localhost",
//          "Port": 5727
//        },
//        {
//          "Host": "localhost",
//          "Port": 5728
//        }
//      ],
//      "UpstreamPathTemplate": "/weather-api/{url}",
//      "UpstreamHttpMethod": [ "Get", "Post" ],
//      "LoadBalancerOptions": {
//        "Type": "RoundRobin"
//        // RoundRobin 轮询 
//        // LeastConnection 最少连接数的服务器
//        // NoLoadBalance 
//      }
//    }
//  ]
//}

////*****************************multiple service instance + Consul********************************
//{
//  "Routes": [
//    {
//      "DownstreamPathTemplate": "/api/{url}", //service address--url variable
//      "DownstreamScheme": "http",
//      "UpstreamPathTemplate": "/consul-api/{url}", //Gateway address
//      "UpstreamHttpMethod": [ "Get", "Post" ],
//      "ServiceName": "MyConsulServices", //consul service name
//      "LoadBalancerOptions": {
//        "Type": "RoundRobin"
//      }
//    }
//  ],
//  "GlobalConfiguration": {
//    "BaseUrl": "http://127.0.0.1:6299",
//    "ServiceDiscoveryProvider": {
//      "Host": "localhost",
//      "Port": 8500,
//      "Type": "Consul"
//    }
//  }
//}

//*****************************Consul+Polly********************************
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/{url}",
      "DownstreamScheme": "http",
      "UpstreamPathTemplate": "/consul/{url}",
      "UpstreamHttpMethod": [ "Get", "Post" ],
      "ServiceName": "MyConsulServices",
      "LoadBalancerOptions": {
        "Type": "RoundRobin"
      },
      "UseServiceDiscovery": true,
      //熔断
      //"QoSOptions": {
      //  "ExceptionsAllowedBeforeBreaking": 3, 
      //  "DurationOfBreak": 10000,
      //  "TimeoutValue": 10000
      //}
       
      //限流
      //"RateLimitOptions": {
      //  "ClientWhitelist": [],
      //  "EnableRateLimiting": true,
      //  "Period": "5m", //1s, 5m, 1h, 1d
      //  "PeriodTimespan": 5,
      //  "Limit": 5
      //}
      //"FileCacheOptions": {
      //  "TtlSeconds": 10
      //}
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://127.0.0.1:6299",
    "ServiceDiscoveryProvider": {
      "Host": "localhost",
      "Port": 8500,
      "Type": "Consul"
    }
    //"RateLimitOptions": {
    //  "QuotaExceededMessage": "Too many requests, maybe later? 11",
    //  "HttpStatusCode": 666 
    //}
  }
}

```

##### 修改Program.cs文件来读取Ocelot的配置
```
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        // read configuration.json instead of appsettings.json file
        .ConfigureAppConfiguration(c => {
            c.AddJsonFile("configuration.json", optional: false, reloadOnChange: true);
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```
##### 修改Startup.cs文件
```
...
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Polly;
...

namespace MicroServiceGateway
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddOcelot();
            services.AddOcelot().AddConsul().AddPolly();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // dotnet MicroServiceGateway.dll --urls="http://*:6299" --ip="127.0.0.1" --port=6299
            app.UseOcelot();
        }
    }
}

```

##### 测试
- 启动网关
```
dotnet MicroServiceGateway.dll --urls="http://*:6299" --ip="127.0.0.1" --port=6299
```
- 使用如下地址访问WebApi
```
http://localhost:6299/consul/WeatherForecast
```
- 在log里面应该是可以观察到真正的api调用情况
```
info: Ocelot.Requester.Middleware.HttpRequesterMiddleware[0]
      requestId: 0HM67HC2VG2MF:00000021, previousRequestId: no previous request id, message: 200 (OK) status code, request uri: http://desktop-mtsuc73:5727/api/WeatherForecast
info: Ocelot.Requester.Middleware.HttpRequesterMiddleware[0]
      requestId: 0HM67HC2VG2MF:00000023, previousRequestId: no previous request id, message: 200 (OK) status code, request uri: http://desktop-mtsuc73:5728/api/WeatherForecast
info: Microsoft.Hosting.Lifetime[0]
```
