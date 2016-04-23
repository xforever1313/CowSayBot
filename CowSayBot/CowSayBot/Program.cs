
//          Copyright Seth Hendrick 2016.
// Distributed under the Boost Software License, Version 1.0.
//    (See accompanying file ../../LICENSE_1_0.txt or copy at
//          http://www.boost.org/LICENSE_1_0.txt)

using System;
using GenericIrcBot;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;

namespace CowSayBot
{
    class MainClass
    {
        private const string nick = "CowSayBot";

        /// <summary>
        /// Regex to look for while in IRC.
        /// </summary>
        private const string cowsayRegex = @"!cowsay\s+(?<cowsayMsg>.+)";

        /// <summary>
        /// Path to the cowsay binary.
        /// </summary>
        private static readonly string cowsayProgram = "/usr/bin/cowsay";

        /// <summary>
        /// The event to wait on until we exit.
        /// </summary>
        private static ManualResetEvent exitEvent = new ManualResetEvent( false );

        private static readonly UnixSignal[] signalsToWatch = new UnixSignal[] {
            new UnixSignal( Signum.SIGTERM ), // Termination Signal
            new UnixSignal( Signum.SIGINT )   // Interrupt from the keyboard
        };

        public static int Main( string[] args )
        {
            if( File.Exists( cowsayProgram ) == false )
            {

                Console.WriteLine( "Cowsay not installed in " + cowsayProgram + ".  Aborting." );
                return 1;
            }

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

            // Generate configs.

            List<IIrcHandler> configs = new List<IIrcHandler>();

            IIrcHandler cowSayConfig = 
                new MessageHandler(
                    cowsayRegex,
                    HandleCowsayCommand,
                    5,
                    ResponseOptions.RespondOnlyToChannel
                );

            configs.Add( cowSayConfig );

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

        /// <summary>
        /// Handles the cowsay command.
        /// </summary>
        /// <param name="writer">The IRC Writer to write to.</param>
        /// <param name="response">The response from the channel.</param>
        private static void HandleCowsayCommand( IIrcWriter writer, IrcResponse response )
        {
            try
            {
                Match cowMatch = Regex.Match( response.Message, cowsayRegex );
                if( cowMatch.Success )
                {
                    string messageToCowsay = cowMatch.Groups["cowsayMsg"].Value;

                    // Run the cowsay subprocess.
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.RedirectStandardInput = true;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.UseShellExecute = false;
                    startInfo.FileName = cowsayProgram;

                    string cowSayedMessage = string.Empty;
                    using( Process cowsayProc = Process.Start( startInfo ) )
                    {
                        using( StreamReader stdout = cowsayProc.StandardOutput )
                        {
                            using( StreamWriter stdin = cowsayProc.StandardInput )
                            {
                                stdin.Write( messageToCowsay );
                                stdin.Flush();
                            }

                            cowSayedMessage = stdout.ReadToEnd();
                        }

                        // If we hang for more than 15 seconds, abort.
                        if( cowsayProc.WaitForExit( 15 * 1000 ) == false )
                        {
                            cowsayProc.Kill();
                        }
                    }

                    if( string.IsNullOrEmpty( cowSayedMessage ) == false )
                    {
                        writer.SendCommand( cowSayedMessage );
                    }
                }
                else
                {
                    Console.WriteLine( "Saw unknown line:" + response.Message );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( "*********************" );
                Console.WriteLine( "Caught Exception:" );
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( "**********************" );
            }
        }
    }
}
