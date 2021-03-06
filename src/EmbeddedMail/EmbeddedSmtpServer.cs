using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EmbeddedMail
{
    public interface ISmtpServer : IDisposable
    {
        IEnumerable<MailMessage> ReceivedMessages();
        void Start();
        void Stop();
    }

    public class EmbeddedSmtpServer : ISmtpServer
    {
        private readonly IList<MailMessage> _messages = new List<MailMessage>();
        private bool _closed;
        public EmbeddedSmtpServer(int port = 25)
            : this(IPAddress.Any, port)
        {
        }

        public EmbeddedSmtpServer(IPAddress address, int port = 25)
        {
            Address = address;
            Port = port;

            Listener = new TcpListener(Address, port);
        }

        public TcpListener Listener { get; private set; }
        public IPAddress Address { get; private set; }
        public int Port { get; private set; }

        public IEnumerable<MailMessage> ReceivedMessages()
        {
            return _messages;
        }

        public void Start()
        {
            Listener.Start();
            SmtpLog.Info(string.Format("Server started at {0}", new IPEndPoint(Address, Port)));
            ListenForClients();
        }

        public void Stop()
        {
            _closed = true;
        }

        public void ListenForClients()
        {
            if (_closed) return;
            ListenForClients(OnClientConnect, e => SmtpLog.Error("Listener socket is closed", e));
        }

        public Task<ISocket> ListenForClients(Action<ISocket> callback, Action<Exception> error)
        {
            Func<IAsyncResult, ISocket> end = r => new SocketWrapper(Listener.EndAcceptSocket(r));
            var task = Task.Factory.FromAsync(Listener.BeginAcceptSocket, end, null);
            task.ContinueWith(t => callback(t.Result), TaskContinuationOptions.NotOnFaulted)
                .ContinueWith(t => error(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            task.ContinueWith(t => error(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }

        public void OnClientConnect(ISocket clientSocket)
        {
            SmtpLog.Info("Client connected");
            ListenForClients();

            var session = new SmtpSession(clientSocket)
            {
                OnMessage = (msg) => _messages.Add(msg)
            };
            session.Start();
        }

        public void Dispose()
        {
            Stop();
            // I don't grok the disposal lifecycle for the sockets yet
            Listener.Stop();
        }
    }
}