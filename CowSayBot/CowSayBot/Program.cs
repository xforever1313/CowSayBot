
//          Copyright Seth Hendrick 2016.
// Distributed under the Boost Software License, Version 1.0.
//    (See accompanying file ../../LICENSE_1_0.txt or copy at
//          http://www.boost.org/LICENSE_1_0.txt)

using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;
using GenericIrcBot;
using CowSayBotPlugin;

namespace CowSayBotMain
{
    class MainClass
    {
        // -------- Fields --------

        /// <summary>
        /// Nick name to use.
        /// </summary>
        private const string nick = "CowSayBot2";

        /// <summary>
        /// The event to wait on until we exit.
        /// </summary>
        private static readonly ManualResetEvent exitEvent = new ManualResetEvent( false );

        /// <summary>
        /// Signals to watch for to terminate teh program.
        /// </summary>
        private static readonly UnixSignal[] signalsToWatch = {
            new UnixSignal( Signum.SIGTERM ), // Termination Signal
            new UnixSignal( Signum.SIGINT )   // Interrupt from the keyboard
        };

        // -------- Functions --------

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code that is given to the operating system after the program ends.</returns>
        public static int Main( string[] args )
        {
            // Start signal detection thread.
            // Once we get a CTRL+C or a sigterm, we will abort the program.
            Thread signalThread =
                new Thread(
                    delegate()
                    {
                        UnixSignal.WaitAny( signalsToWatch );
                        exitEvent.Set();
                    }
                );

            signalThread.Start();

            IrcConfig config = new IrcConfig();
            config.Nick = nick;
            config.Server = "irc.freenode.net";
            config.Channel = "#testcow";
            config.RealName = "Cow Say Bot";
            config.UserName = nick;

            // Initialize Plugins
            List<IIrcHandler> configs = new List<IIrcHandler>();

            // Load cow-say plugin.
            CowSayBot cowSayPlugin = new CowSayBot();
            string error;
            if( cowSayPlugin.Validate( out error ) == false )
            {
                Console.WriteLine( error );
                return 1;
            }

            configs.AddRange( cowSayPlugin.GetHandlers() );

            // Must handle pings.
            configs.Add( new PingHandler() );

            using( IrcBot bot = new IrcBot( config, configs ) )
            {
                bot.Start();
                exitEvent.WaitOne();
                signalThread.Join();
            }

            return 0;
        }
    }
}
