﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PrijemRemont;
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
        public async Task<ActionResult> Submit(int device, double timeInWarehouse, double workHours)
        {

            var myBinding = new NetTcpBinding(SecurityMode.None);
            var myEndpoint = new EndpointAddress("net.tcp://localhost:46300/PrijemRemontEndpoint");

            using (var myChannelFactory = new ChannelFactory<IRemont>(myBinding, myEndpoint))
            {
                IRemont client = null;

                try
                {
                    client = myChannelFactory.CreateChannel();
                    if (await client.SendToRemont(device, timeInWarehouse, workHours))
                    {
                        ViewData["Title"] = "POSLATO NA REMONT";
                    }
                    else
                    {
                        ViewData["Title2"] = "NIJE USPEO REMONT";
                    }
                    ((ICommunicationObject)client).Close();
                    myChannelFactory.Close();
                }
                catch (Exception e)
                {
                    string a = e.Message;
                    (client as ICommunicationObject)?.Abort();
                }
            }
            return View("Index");
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}