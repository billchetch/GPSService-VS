using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Services;
using System.Configuration.Install;
using System.ComponentModel;

namespace GPSService
{
    [RunInstaller(true)]
    public class GPSSInstaller : ServiceInstaller
    {
        public GPSSInstaller() : base("GPSService",
                                    "GPS Service",
                                    "Runs a Chetch Messaging client and a GPS manager")
        {
            //empty
        }
    }
}
