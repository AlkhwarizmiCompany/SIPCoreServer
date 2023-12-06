using Microsoft.Extensions.Configuration;
using SIPServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIPServer.Call
{
    internal abstract  class KHService
    {
        protected SIPCall                   _call;
        protected readonly IConfiguration   _configuration;
        protected CancellationTokenSource   _cancellationTokenSource;

        public KHService(IConfiguration configuration, SIPCall call)
        {
            _call = call;
            _configuration = configuration;

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public abstract void main();

        public virtual async Task Initialization() 
        { 
        }

        public virtual void Finalization()
        {
        }

        public async void Start()
        {
            await Initialization();

            Task.Run(() => main());
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();

            Finalization();
        }

    }
}
