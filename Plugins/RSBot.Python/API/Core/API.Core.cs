using Python.Runtime;
using RSBot.Core;
using RSBot.Core.Network;
using RSBot.Python.Runtime;
using SkiaSharp;
using System;
using System.Linq;

namespace RSBot.Python.API.Core
{
    /// <summary>
    /// Core-Functions for Python like logging, info, char data and start/stop.
    /// </summary>
    public class CoreAPI : IPythonPlugin
    {
        /// <summary>
        /// Unique module name of the plugin.
        /// </summary>
        public string ModuleName => "core";
        private PythonContext _context;
        public void Init(PythonContext context) => _context = context;

        public bool start_bot()
        {
            if (Kernel.Proxy == null || !Kernel.Proxy.IsConnectedToAgentserver || Kernel.Bot == null || Game.Player == null || Kernel.Bot.Running)
                return false;
            Kernel.Bot.Start();
            return true;
        }

        public bool stop_bot()
        {
            if (Kernel.Bot == null || !Kernel.Bot.Running)
                return false;
            Kernel.Bot.Stop();
            return true;
        }

        public bool send_server(ushort opcode, byte[] data, bool encrypted = false)
        {
            if (Kernel.Proxy == null)
                return false;

            Packet packet = new Packet(opcode, encrypted);
            packet.WriteBytes(data);
            PacketManager.SendPacket(packet, PacketDestination.Server);
            return true;
        }

        public bool send_client(ushort opcode, byte[] data, bool encrypted = false)
        {
            if (Kernel.Proxy == null)
                return false;

            Packet packet = new Packet(opcode, encrypted);
            packet.WriteBytes(data);
            PacketManager.SendPacket(packet, PacketDestination.Client);
            return true;
        }

        public void log(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                _context.Log?.Invoke(string.Empty);
                return;
            }

            string msg = string.Join(" ", args.Select(a =>
            {
                if (a == null) return "None";
                if (a is PyObject py)
                {
                    using (Py.GIL())
                        return py.ToString();
                }
                return a.ToString();
            }));

            _context.Log?.Invoke(msg);
        }
    }
}
