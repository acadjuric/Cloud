using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PrijemRemont;
using Publisher;
using Web.Models;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Route("Home/Submit")]
        public async Task<JsonResult> Submit(int device, double timeInWarehouse, double workHours)
        {

            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:46300/PrijemRemontEndpoint");

            using (var myChannelFactory = new ChannelFactory<IRemont>(myBinding, myEndpoint))
            {
                IRemont client = null;

                try
                {
                    string messageResponse = string.Empty;

                    client = myChannelFactory.CreateChannel();

                    if (await client.SendToRemont(device, timeInWarehouse, workHours))
                    {
                        messageResponse = "POSLATO NA REMONT";
                    }
                    else
                    {
                        messageResponse = "NIJE POSLATO NA REMONT";
                    }
                    var uredjaji = await client.GetAllDevices();

                    ((ICommunicationObject)client).Close();
                    myChannelFactory.Close();

                    if (uredjaji == null)
                        return Json(new { message = messageResponse, devices = new List<Device>() });
                    else
                        return Json(new { message = messageResponse, devices = uredjaji });
                }
                catch (Exception e)
                {
                    string a = e.Message;
                    (client as ICommunicationObject)?.Abort();
                    return Json(new { message = e.Message, devices = new List<Device>() });
                }
            }

        }

        [HttpGet]
        [Route("Home/GetData")]
        public async Task<JsonResult> GetData()
        {
            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:46000/PublisherEndpoint");


            using (var myChannelFactory = new ChannelFactory<IPublisher>(myBinding, myEndpoint))
            {
                IPublisher client = null;

                try
                {
                    client = myChannelFactory.CreateChannel();
                    Tuple<List<Remont>, List<Remont>> result = await client.GetRemontAndHistoryRemont();

                    ((ICommunicationObject)client).Close();
                    myChannelFactory.Close();

                    if (result == null)
                        return Json(new { aktivni = new List<Remont>(), istorija = new List<Remont>() });
                    else
                        return Json(new { aktivni = result.Item1, istorija = result.Item2 });
                }
                catch (Exception e)
                {
                    string a = e.Message;
                    (client as ICommunicationObject)?.Abort();
                    return Json(new { aktivni = new List<Remont>(), istorija = new List<Remont>() });
                }
            }

        }



        [HttpGet]
        [Route("Home/GetDevices")]
        public async Task<JsonResult> GetDevices()
        {
            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:46000/PublisherEndpoint");


            using (var myChannelFactory = new ChannelFactory<IPublisher>(myBinding, myEndpoint))
            {
                IPublisher client = null;

                try
                {
                    client = myChannelFactory.CreateChannel();
                    List<Device> result = await client.GetDevices();

                    ((ICommunicationObject)client).Close();
                    myChannelFactory.Close();

                    if (result == null)
                        return Json(new List<Device>());
                    else
                        return Json(result);
                }
                catch (Exception e)
                {
                    string a = e.Message;
                    (client as ICommunicationObject)?.Abort();
                    return Json(new List<Device>());
                }
            }

        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
