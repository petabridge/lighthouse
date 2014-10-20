using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Lighthouse.Service
{
    [RunInstaller(true)]
    public partial class LighthouseServiceInstaller : System.Configuration.Install.Installer
    {
        public LighthouseServiceInstaller()
        {

            var serviceProcessInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            //# Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            //# Service Information
            serviceInstaller.DisplayName = "Lighthouse Service Discovery";
            serviceInstaller.Description = "Automatic service discovery for Akka.NET clusters";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // This must be identical to the 
            // WindowsService.ServiceBase name
            // set in the constructor of WindowsService.cs
            serviceInstaller.ServiceName = "LighthouseService";
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);


            InitializeComponent();
        }
    }
}
