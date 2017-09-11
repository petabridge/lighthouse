# Lighthouse

**Lighthouse** is a simple service-discovery tool for Akka.Cluster, designed to make it easier to place nice with PaaS deployments like Azure / Elastic Beanstalk / AppHarbor.

## Running on .NET 4.5

Lighthouse runs on [Akka.NET](https://github.com/akkadotnet/akka.net) version 1.3.1, which supports .NET 4.5 and .NET Core 1.1/.NET Standard 1.6.  To package the executable and run the .NET 4.5 version locally, clone this repo and build the `Lighthouse` project.  Running Lighthouse.exe in a console should produce an output similar to this:

```
Topshelf.HostFactory: Configuration Result:
[Success] Name Lighthouse
[Success] DisplayName Lighthouse Service Discovery
[Success] Description Lighthouse Service Discovery for Akka.NET Clusters
[Success] ServiceName Lighthouse
Topshelf.HostConfigurators.HostConfiguratorImpl: Topshelf v3.2.150.0, .NET Framework v4.0.30319.42000
```

The Lighthouse .NET 4.5 project is built as a [Topshelf](https://github.com/Topshelf/Topshelf) service.  This allows you to install Lighthouse as a Windows Service using a command like this:

```
Lighthouse.exe install --localsystem --autostart
```

See the Topshelf documentation for more info on command line arguments for installing a Topshelf service.

# Running on .NET Core

Lighthouse also includes a .NET Core-compatible version under a separate project named `Lighthouse.NetCoreApp`.  This project does not build as a Topshelf web service.  You have 2 ways that you can run this version:

- using the .NET CLI
- building the project as a standalone .exe for your specific [runtime identifier](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)

#### Using the .NET CLI

Build the project either in Visual Studio 2017 or using `dotnet build -c Release Lighthouse.NetCoreApp.csproj`.  This will output `Lighthouse.NetCoreApp.dll` in your `bin/Release` folder.  From there, running `dotnet ./Lighthouse.NetCoreApp.dll` will start Lighthouse.  Pressing `Enter` will exit.

#### Building the project as an .exe

You need to restore the dependencies for the target runtime identifier that you want to build the executable for:

```
dotnet restore -r win7-x64
```

Then, you may publish the executable using the command:

```
dotnet publish -c Release -r win7-x64 -f netcoreapp1.1
```

This will include a `publish` folder in your bin directory that will include the .exe and the .NET Core runtime dependencies:

```
bin/
	Release/
		netcoreapp1.1/
			win7-x64/
				publish/
					Lighthouse.NetCoreApp.exe
```