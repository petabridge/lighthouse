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
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            this.serviceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.serviceInstaller = new System.ServiceProcess.ServiceInstaller();

            //# Service Information
            serviceInstaller.DisplayName = "Lighthouse Service Discovery";
            serviceInstaller.Description = "Automatic service discovery for Akka.NET clusters";
            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
            serviceInstaller.AfterInstall += (sender, args) =>
            {
                var sc = new ServiceController("Lighthouse");
                sc.Start();
            };

            InitializeComponent();
        }
    }
}
