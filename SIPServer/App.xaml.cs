using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIPServer.Call;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace SIPServer
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider serviceProvider;
        public IConfiguration Configuration { get; private set; }

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Build configuration
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = configurationBuilder.Build();

            // Register configuration
            services.AddSingleton<IConfiguration>(Configuration);

            // Register other services
            // services.AddTransient<IMyService, MyService>();
            services.AddTransient<CallManager>();
            services.AddTransient<SpeechToText>();
            services.AddTransient<Chatbot>();
            services.AddTransient<TextToSpeech>();

            services.AddSingleton<Server>();
            services.AddSingleton<MainWindow>();

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = serviceProvider.GetService<MainWindow>(); // Resolving using DI
            mainWindow.Show();
        }

    }

}
