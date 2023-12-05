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
        protected SIPCall _call;
        protected readonly IConfiguration _configuration;
        protected CancellationTokenSource cancellationTokenSource;

        public KHService(IConfiguration configuration, SIPCall call)
        {
            _call = call;
            _configuration = configuration;
            
            cancellationTokenSource = new CancellationTokenSource();
        }

        public abstract void main();

        public void Start()
        {
            Task.Run(() => main());
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
        }

    }
}
