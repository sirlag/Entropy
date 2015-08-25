using System;
using SteamKit2;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Net;
using System.Linq;

namespace Entropy
{
    class Program
    {
        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;

        static bool isRunning;

        static string user, pass;
        static string authCode, twoFactorAuth;

        static string steamApiKey;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Sample5: No username and password specified!");
                return;
            }

            // save our logon details
            user = args[0];
            pass = args[1];

            getApiKey();

            // create our steamclient instance
            steamClient = new SteamClient();
            // create the callback manager which will route callbacks to function calls
            manager = new CallbackManager(steamClient);

            // get the steamuser handler, which is used for logging on after successfully connecting
            steamUser = steamClient.GetHandler<SteamUser>();

            // register a few callbacks we're interested in
            // these are registered upon creation to a callback manager, which will then route the callbacks
            // to the functions specified
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            // this callback is triggered when the steam servers wish for the client to store the sentry file
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);

            isRunning = true;

            Console.WriteLine("Connecting to Steam...");

            // initiate the connection
            steamClient.Connect();

            // create our callback handling loop
            while (isRunning)
            {
                // in order for the callbacks to get routed, they need to be handled by the manager
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam: {0}", callback.Result);

                isRunning = false;
                return;
            }

            Console.WriteLine("Connected to Steam! Logging in '{0}'...", user);

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                // if we have a saved sentry file, read and sha-1 hash it
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authCode,
                TwoFactorCode = twoFactorAuth,
                SentryFileHash = sentryHash,
            });
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
           //Console.WriteLine("Disconnected from Steam, reconnecting in 5...");

            //Thread.Sleep(TimeSpan.FromSeconds(5));

            // steamClient.Connect();
            Environment.Exit(1);
        }

        static void OnLoggedOn( SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result ==  EResult.AccountLoginDeniedNeedTwoFactor;

            if ( isSteamGuard || is2FA)
            {
                Console.WriteLine("Unable to logon to Steam: This account is SteamGuard protected.");
                
                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticator app: ");
                    twoFactorAuth = Console.ReadLine();
                }      
                else
                {
                    Console.Write("Please enter the auth code sent to the email at {0}:", callback.EmailDomain);
                    authCode = Console.ReadLine();
                }

                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                isRunning = false;
                return;
            }

            Console.WriteLine("Successfully logged on!");

            using (dynamic steamPlayerServices = WebAPI.GetInterface("IPlayerService", steamApiKey))
            {

                try
                {
                    KeyValue kvGames = steamPlayerServices.GetOwnedGames(steamid: steamUser.SteamID.ConvertToUInt64(), include_appinfo: 1);
                    var apps = SteamDrives.getAllInstalledGames();
                    foreach (KeyValue game in kvGames["games"].Children)
                    {
                        if (apps.Contains(game["appid"].AsInteger()))
                            Console.WriteLine("{0}", game["name"].AsString());
                    }
                }
                catch (WebException ex)
                {
                    Console.WriteLine("Unable to make API request:{0}", ex.Message);
                }
            }

            steamUser.LogOff();
            return;
        }

        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }

        static void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");

            // write out our sentry file
            // ideally we'd want to write to the filename specified in the callback
            // but then this sample would require more code to find the correct sentry file to read during logon
            // for the sake of simplicity, we'll just use "sentry.bin"

            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });

            Console.WriteLine("Done!");
        }

        static void getApiKey()
        {
            string path = ".apikey";

            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    
                    var NewApiKey = "";
                    Console.WriteLine(@"There is no steam API key set. To get an api key, vist https://steamcommunity.com/dev/apikey.");

                    while (NewApiKey.Length != 32)
                    {
                        Console.WriteLine("Please enter your API key : ");
                        NewApiKey = Console.ReadLine();
                        if (NewApiKey.Length != 32)
                            Console.WriteLine("Your API key is invalid, please enter a valid 32 character API key.");
                    }

                    AddText(fs, NewApiKey);
                }
            }

            var apikey = "";
            using (var fs = File.Open(".apikey", FileMode.Open, FileAccess.Read))
            {
                byte[] b = new byte[1024];
                UTF8Encoding temp = new UTF8Encoding(true);
                fs.Read(b, 0, b.Length);
                apikey = temp.GetString(b);
            }
            steamApiKey = apikey;
        }

        private static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Write(info, 0, info.Length);
        }
    }
}
