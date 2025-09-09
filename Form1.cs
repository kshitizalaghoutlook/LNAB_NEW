using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace LNAB
{
    [ComVisible(true)]
    public partial class Form1 : Form
    {
        string strPrev = "";
        private string conn = ConfigurationManager.ConnectionStrings["LNABService.Properties.Settings.connStr"].ConnectionString;
        private ServiceHost myServiceHost;
        private int Exception;
        private string host;
        private static int port = 2207;
        private string app;
        private string appName = string.Concat(new string[] { "net.tcp://", Environment.MachineName, ":", port.ToString(), "/LNAB" });
        private string machineName = Environment.MachineName;
        private string vin = "";
        private bool prevSearch;
        private bool loggedIn;
        private short liens;
        private static IBrowser browser;
        private static IPage page;
        bool newRequest = false;
        private HttpListener listener;
        private string lastReceivedRequest = "";

        // schedule + one precise boundary timer (no OnTimerEvent)
        private SupplierSchedule _sched;
        private readonly System.Windows.Forms.Timer _boundaryTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _idleTimer = new System.Windows.Forms.Timer();
        private DateTime _lastActivity;
        private ScheduleGate _restartGate;


        string[] errorTerms = { "TIMEOUT", "due to planned maintenance", "the appres system is temporarily unavailable", "asas id enrollment not found" };

        private string currentReqID;
        //private Stopwatch stopwatch;

        //private Timer t = new Timer();

        //private Timer t1 = new Timer();

        //private Timer tProgress = new Timer();

        //private DateTime searchStart;

        //private DateTime searchStop;

        private Queue<Func<Task>> requestQueue = new Queue<Func<Task>>();
        private bool isProcessing = false;
        private readonly object lockObj = new object();


        //test code chatgpt below


        // --- helpers (put them in the same class or make them static somewhere) ---
        //  private async Task<ElementHandle> ShadowAsync(IPage page, string hostSel, string innerSel)
        //  {
        //      await page.WaitForFunctionAsync(
        //          "(h,i)=>{const e=document.querySelector(h);return e&&e.shadowRoot&&e.shadowRoot.querySelector(i)}",
        //          null, hostSel, innerSel);

        //      var handle = await page.EvaluateFunctionHandleAsync(
        //          "(h,i)=>document.querySelector(h).shadowRoot.querySelector(i)", hostSel, innerSel);

        //      return (ElementHandle)handle;
        //  }

        //  private async Task<ElementHandle> ShadowButtonByTextAsync(IPage page, string text)
        //  {
        //      var handle = await page.EvaluateFunctionHandleAsync(@"
        //t => {
        //  const hosts = Array.from(document.querySelectorAll('goa-button'));
        //  for (const h of hosts) {
        //    const b = h.shadowRoot && h.shadowRoot.querySelector('button');
        //    if (!b) continue;
        //    const s = (b.textContent || '').trim().toLowerCase();
        //    if (s.indexOf(t.toLowerCase()) >= 0) return b;
        //  }
        //  return null;
        //}", text);
        //      return handle as ElementHandle;
        //  }

        //  // Wait for a real navigation OR for a selector (SPA) – returns true if selector matched
        //  private async Task<bool> WaitNavOrSelectorAsync(IPage page, string selector, int timeoutMs)
        //  {
        //      var navTask = page.WaitForNavigationAsync(new NavigationOptions
        //      {
        //          WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
        //          Timeout = timeoutMs
        //      });
        //      var selTask = page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Timeout = timeoutMs });
        //      var done = await Task.WhenAny(navTask, selTask);
        //      return done == selTask;
        //  }

        //  // ============== REPLACEMENT LOGIN ==============
        //  private async Task<(IPage, bool)> Login(IPage page)
        //  {
        //      try
        //      {
        //          // 1) Go to login
        //          await page.GoToAsync("https://appres.alberta.ca/GOA.APPRES.Login/Login_Alberta.aspx",
        //              new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 60000 });

        //          // 2) Click "Login with MADI" if present (this step usually navigates)
        //          var madi = await page.QuerySelectorAsync("#btnLgnUsingMADI");
        //          if (madi != null)
        //          {
        //              var navTask = page.WaitForNavigationAsync(new NavigationOptions
        //              {
        //                  WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
        //                  Timeout = 60000
        //              });
        //              await madi.ClickAsync();
        //              await navTask;
        //          }

        //          // 3) USERNAME (shadow DOM)
        //          // Focus the real <input> inside <goa-input>, type username, press Enter
        //          var userInput = await ShadowAsync(page, "goa-input", "input");
        //          await userInput.FocusAsync();
        //          await page.Keyboard.TypeAsync("UCDAON", new TypeOptions { Delay = 80 });
        //          await page.Keyboard.PressAsync("Enter");

        //          // 4) Wait for PASSWORD field (SPA step) or error banner
        //          var pwOrErr = await Task.WhenAny(
        //              page.WaitForSelectorAsync("goa-input input[type='password']", new WaitForSelectorOptions { Timeout = 60000 }),
        //              page.WaitForSelectorAsync("div.error-msg[role='alert']", new WaitForSelectorOptions { Timeout = 60000 })
        //          );
        //          if (pwOrErr != null && pwOrErr != null && pwOrErr == null) { } // no-op to satisfy compiler warnings

        //          // If error banner showed up, throw with its text
        //          var errVisible = await page.EvaluateFunctionAsync<bool>(
        //              "sel => !!document.querySelector(sel)", "div.error-msg[role='alert']");
        //          if (errVisible)
        //          {
        //              var msg = await page.EvaluateFunctionAsync<string>(
        //                  "sel => (document.querySelector(sel)?.textContent||'').trim()", "div.error-msg[role='alert']");
        //              throw new Exception("Username step failed: " + msg);
        //          }

        //          // 5) PASSWORD (shadow DOM)
        //          var passInput = await ShadowAsync(page, "goa-input", "input[type='password']");
        //          await passInput.FocusAsync();
        //          await page.Keyboard.TypeAsync("K1llB1ll", new TypeOptions { Delay = 80 });

        //          // Click the real **Sign in** button (not "Forgot password?")
        //          var signInBtn = await ShadowButtonByTextAsync(page, "Sign in");
        //          if (signInBtn == null)
        //          {
        //              // Fallback: the full-width button on this screen is typically "Sign in"
        //              signInBtn = await ShadowAsync(page, "goa-button[width]", "button");
        //          }
        //          await signInBtn.ClickAsync();

        //          // 6) Final landing: redirect chain or SPA shell
        //          // Consider "ready" when any of these appear:
        //          var readySelector =
        //              "iframe[src*='GOA.APPRES.Gateway/ISDFrameMain.htm'], iframe[src*='GOA.APPRES.Web/Service/SearchRequest.aspx'], app-shell, [data-testid='dashboard']";

        //          var landed = await WaitNavOrSelectorAsync(page, readySelector, 60000);
        //          if (!landed)
        //          {
        //              // Double-check selector after nav race; if still not ready, fail
        //              var ready = await page.EvaluateFunctionAsync<bool>(
        //                  "sel => !!document.querySelector(sel)", readySelector);
        //              if (!ready) throw new Exception("Post-login did not reach a ready state.");
        //          }

        //          this.loggedIn = true;
        //          return (page, true);
        //      }
        //      catch (Exception ex)
        //      {
        //          Log("Login error: " + ex.Message);
        //          this.InActivate();
        //          CloseServiceHost();
        //          start();
        //          return (page, false);
        //      }
        //  }
        //test code chatgpt ends



        public void EnqueueRequest(Func<Task> request)
        {
            lock (lockObj)
            {
                requestQueue.Enqueue(request);
                if (!isProcessing)
                {
                    isProcessing = true;
                    ProcessQueue();
                }
            }
            ResetIdleTimer();
        }

        private async void ProcessQueue()
        {
            while (requestQueue.Count > 0)
            {
                Func<Task> request;
                lock (lockObj)
                {
                    request = requestQueue.Dequeue();
                }

                try
                {
                    await request();
                    ResetIdleTimer();
                }
                catch (Exception exception1)
                {
                    Log($"Error: {exception1.Message}");
                    await Task.Delay(3000); // wait before retrying
                    await page.GoToAsync("https://appres.alberta.ca/GOA.APPRES.Login/Login_Alberta.aspx");
                }
            }

            lock (lockObj)
            {
                isProcessing = false;
            }
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            if (!isProcessing && DateTime.Now - _lastActivity >= TimeSpan.FromMinutes(30))
            {
                Application.Restart();
            }
        }

        private void ResetIdleTimer()
        {
            _lastActivity = DateTime.Now;
            _idleTimer.Stop();
            _idleTimer.Start();
        }

        public Form1()
        {
            //this.t = new System.Windows.Forms.Timer();
            //this.tProgress = new System.Windows.Forms.Timer();
            //this.wb = new WebBrowser();
            //this.vin = "";
            //this.machineName = SystemInformation.ComputerName;
            //this.conn = ConfigurationManager.ConnectionStrings["LNABService.Properties.Settings.connStr"].ConnectionString;
            //string[] textArray1 = new string[] { "net.tcp://", SystemInformation.ComputerName, ":", Settings.Default.port.ToString(), "/LNAB" };
            //this.appName = string.Concat(textArray1);
            //////this.end = new TimeSpan(Convert.ToInt16(Settings.Default.Closing), 0, 0);
            ///
            if (this.Controls.Count == 0) // If no controls are loaded, initialize them
            {
                InitializeComponent();
                //   StartHttpListener();
            }
            //this.InitializeComponent();

            //this.webBrowser1.ObjectForScripting = this;
            base.Closing += new CancelEventHandler(this.Form1_Closing);

            _idleTimer.Interval = (int)TimeSpan.FromMinutes(30).TotalMilliseconds;
            _idleTimer.Tick += IdleTimer_Tick;
            _lastActivity = DateTime.Now;
            _idleTimer.Start();

        }

        private async void StartHttpListener()
        {

            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            if (listener.IsListening)
            {
                //listener.Close();
                return;
            }
            else
            {
                listener.Start();
            }


            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest request = context.Request;

                using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                {
                    lastReceivedRequest = await reader.ReadToEndAsync();
                    preStart();
                }

                // Update UI thread
                Invoke(new Action(() => listBox1.Items.Add("Received: " + lastReceivedRequest)));

                // Send response
                HttpListenerResponse response = context.Response;
                byte[] buffer = Encoding.UTF8.GetBytes("Request Received");
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
        }

        protected string LoadNew()
        {



            string str = "";

            string str1 = this.ReportCompletedSearchesNotDistributed();
            if (str1 == "")
            {

                if (this.searchOpen())
                {
                    start();
                }
            }
            else
            {
                try
                {
                    markBusy();
                    //this.vin = str1.Substring(str1.IndexOf("_") + 1);
                    //this.currentReqID = str1.Substring(0, str1.IndexOf("_"));
                    this.prevSearch = true;
                    str = str1;
                }
                catch (System.Exception exception2)
                {
                    //sendEmail(exception2.ToString());
                    this.InActivate();
                    this.myServiceHost.Close();
                    // this.InActivate();
                    // throw;
                }
            }
            return str;
        }

        //private void Form1_Load(object sender, EventArgs e)
        //{

        //}
        //private void Form1_Load(object sender, EventArgs e)
        //{

        //    //StartHttpListener();

        //    Form1 queue = new Form1();




        //    string str = "";
        //    string[] strArrays = this.appName.Split(new char[] { '/' });
        //    string[] strArrays1 = strArrays[2].Split(new char[] { ':' });
        //    this.host = strArrays1[0];
        //    port = Convert.ToInt16(strArrays1[1]);
        //    this.app = strArrays[3];
        //    this.Activate();
        //    Uri uri = new Uri(this.appName);

        //    //Service Host Started
        //    try
        //    {
        //        this.myServiceHost = new ServiceHost(typeof(PatientService), new Uri[] { uri });


        //        // Check if ServiceHost is opened and working
        //        if (this.myServiceHost.State == CommunicationState.Opened)
        //        {
        //            Console.WriteLine("ServiceHost is working and accepting requests.");
        //        }
        //        //else if(this.myServiceHost.State == CommunicationState.Created)
        //        //{
        //        //    Console.WriteLine("ServiceHost is already created and accepting requests.");
        //        //}
        //        else
        //        {

        //            this.myServiceHost.Open();

        //        }

        //        //this.myServiceHost.Close();
        //    }
        //    catch (System.Exception exception1)
        //    {
        //        System.Exception exception = exception1;
        //        listBox1.Items.Add($"Exception: {exception.Message}");
        //        //sendEmail(exception.ToString());
        //        //Console.WriteLine(exception.ToString());
        //        this.InActivate();
        //        CloseServiceHost();
        //      //  this.myServiceHost.Close();
        //       // throw;
        //    }

        //    //string str1 = this.ReportCompletedSearchesNotDistributed();
        //    //if (str1 == "")
        //    //{

        //        if (this.searchOpen())
        //        {
        //            start();
        //        }
        //    //}
        //    //else
        //    //{
        //    //    try
        //    //    {
        //    //        markBusy();
        //    //        //this.vin = str1.Substring(str1.IndexOf("_") + 1);
        //    //        //this.currentReqID = str1.Substring(0, str1.IndexOf("_"));
        //    //        this.prevSearch = true;
        //    //        str = str1;
        //    //    }
        //    //    catch (System.Exception exception2)
        //    //    {
        //    //        sendEmail(exception2.ToString());
        //    //        this.InActivate();
        //    //        throw;
        //    //    }
        //    //}
        //    //if (str.Length > 0)
        //    //{
        //    //    queue.EnqueueRequest(() => ProcessRequest(str, prevSearch));
        //    //}
        //    //else
        //    //{
        //    //    queue.EnqueueRequest(() => ProcessRequest("", false));
        //    //}
        //    //  return str;


        //}

        private void Form1_Load(object sender, EventArgs e)
        {
            // === Load SupplierSchedule from DB ===
            _sched = null;
            using (var sqlConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["LNABService.Properties.Settings.connStr"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT Value FROM AppConfig WHERE [key]='LNAB_Schedule'", sqlConnection))
            {
                sqlConnection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var raw = Convert.ToString(reader["Value"] ?? "");
                        _sched = new SupplierSchedule(raw.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                }
            }

            // Test schedule: force a 4:50 PM start with a 4:55 PM stop
            _sched = new SupplierSchedule(new[] { $"{(int)DateTime.Now.DayOfWeek};4:50 PM;4:55 PM" });

            if (_sched == null)
            {
                // No schedule -> treat as closed and retry soon
                this.InActivate();
                _boundaryTimer.Stop();
                _boundaryTimer.Tick -= BoundaryTimer_Tick;
                _boundaryTimer.Interval = 60_000; // 1 minute retry
                _boundaryTimer.Tick += BoundaryTimer_Tick;
                _boundaryTimer.Start();
                return;
            }

            _restartGate?.Dispose();
            _restartGate = new ScheduleGate(_sched);
            _restartGate.Start();

            // === Decide current state ===
            var now = DateTime.Now;
            var w = GetWindow(_sched, now.DayOfWeek);
            var inside = InWindow(w, now.TimeOfDay);

            // ===== Your existing code (unchanged) =====
            //StartHttpListener();

            Form1 queue = new Form1();

            string str = "";
            string[] strArrays = this.appName.Split(new char[] { '/' });
            string[] strArrays1 = strArrays[2].Split(new char[] { ':' });
            this.host = strArrays1[0];
            port = Convert.ToInt16(strArrays1[1]);
            this.app = strArrays[3];
            this.Activate();
            Uri uri = new Uri(this.appName);

            // Service Host Started
            try
            {
                this.myServiceHost = new ServiceHost(typeof(PatientService), new Uri[] { uri });

                if (this.myServiceHost.State != CommunicationState.Opened)
                {
                    this.myServiceHost.Open();
                }
            }
            catch (System.Exception exception1)
            {
                listBox1.Items.Add($"Exception: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
            }

            if (inside)
            {
                if (this.searchOpen())
                {
                    start();
                }
            }
            else
            {
                // outside hours right now
                this.InActivate();
                // do not start work now; wait for boundary timer
            }

            // === Arm the boundary timer to flip state at the next start/stop ===
            ArmBoundaryTimer();
        }


        static async Task ProcessRequest(string newRequest, bool prevSearch)
        {



            string browserRelativePath = @"Chrome\Win64-124.0.6367.201\chrome-win64\chrome.exe"; // relative path inside your project
            string executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, browserRelativePath);

            Form1 program = new Form1();
            program.listBox1.Items.Add(newRequest);
            //AppDomain.CurrentDomain.ProcessExit += new EventHandler(program.OnProcessExit);
            try
            {



                //// Check if the browser is open
                //            if (browser != null && browser.IsConnected)
                //            {
                //                Console.WriteLine("Browser is open.");
                //            }
                //            else
                //            {  // Download Chromium if not already present
                //                //await new BrowserFetcher().DownloadAsync();
                //                // Launch the browser

                //                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                //                {
                //                    Headless = false, // Set to true if you want to run headless
                //                    //ExecutablePath = @"C:\Program Files (x86)\UCDA\LNAB\Chrome\Win64-124.0.6367.201\chrome-win64\chrome.exe",
                //                    ExecutablePath = @"D:\Users\Kshitiz\source\repos\LNAB\bin\Debug\Chrome\Win64-124.0.6367.201\chrome-win64\chrome.exe",
                //                    Args = new[] {
                //    "--incognito",
                //    "--no-sandbox",
                //    "--disable-gpu",
                //    "--disable-dev-shm-usage",
                //    "--disable-features=site-per-process",
                //    "--disable-blink-features=AutomationControlled"
                //}
                //                });
                //                // Open a new page
                //                page = await browser.NewPageAsync();
                //                // Clear cookies before navigating to the page
                //                //var client = await page.CreateCDPSessionAsync();
                //                //await client.SendAsync("Network.clearBrowserCookies");
                //                //BrowserContext context = await browser.CreateIncognitoBrowserContextAsync();
                //                //page = await context.NewPageAsync();

                //            }


                //chatgpt code below
                if (browser != null && browser.IsConnected)
                {
                    Console.WriteLine("Browser is open.");

                    // Reuse the first page; close any extras
                    var pages = await browser.PagesAsync();
                    if (pages.Length == 0)
                        page = await browser.NewPageAsync();
                    else
                    {
                        page = pages[0];
                        for (int i = 1; i < pages.Length; i++)
                            await pages[i].CloseAsync();
                    }
                }
                else
                {
                    browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = false,
                        ExecutablePath = executablePath,
                        Args = new[] {
            "--incognito",
            "--no-sandbox",
            "--disable-gpu",
            "--disable-dev-shm-usage",
            "--disable-features=site-per-process",
            "--disable-blink-features=AutomationControlled"
        }
                    });

                    // Use the first incognito tab Chrome starts with; close extras
                    var pages = await browser.PagesAsync();
                    if (pages.Length == 0)
                        page = await browser.NewPageAsync();
                    else
                    {
                        page = pages[0];
                        for (int i = 1; i < pages.Length; i++)
                            await pages[i].CloseAsync();
                    }
                }


                //chagpt code ends


                string email = ConfigurationManager.AppSettings["Email"];



                string request = "";

                var frames = page.Frames;
                // Find the frame by its src attribute
                var iframeSrc = "https://appres.alberta.ca/GOA.APPRES.Web/Service/SearchRequest.aspx"; // Replace with the actual src of the iframe

                var iframeSrc2 = "https://appres.alberta.ca/GOA.APPRES.Gateway/ISDFrameMain.htm"; // Replace with the actual src of the iframe



                var targetFrame = frames.FirstOrDefault(frame => frame.Url.Contains(iframeSrc));

                var targetFrame2 = frames.FirstOrDefault(frame => frame.Url.Contains(iframeSrc2));

                if (targetFrame != null)
                {
                    program.loggedIn = true;
                    //Check if error occurs

                    // Search for any of the words or sentences inside the iframe
                    string pageContent = await targetFrame.EvaluateFunctionAsync<string>("() => document.body.innerText");

                    foreach (var term in program.errorTerms)
                    {
                        if (pageContent.Contains(term))
                        {
                            throw new Exception($"Error found: {term}");
                        }
                    }
                }
                else if (targetFrame2 != null)
                {
                    program.loggedIn = true;
                }

                if (program.loggedIn)
                {

                    Console.WriteLine("Logged in");

                    request = newRequest;

                    Log("Request:- " + request);
                    if (request != "")
                    {
                        program.newRequest = true;
                    }
                    else
                    {
                        program.markAvailable();
                    }
                }
                else
                {
                    //request = program.LoadNew();
                    request = newRequest;
                    //  MessageBox.Show("line 337");
                    await program.Login(page);
                }
                var tempVin = "";
                if (prevSearch)
                {
                    //string str = program.nextVINReq();
                    program.vin = request.Substring(request.IndexOf("_") + 1);
                    tempVin = program.vin;
                    if (tempVin != "")
                    {
                        program.currentReqID = request.Substring(0, request.IndexOf("_"));
                        var tempcurrentReqID = program.currentReqID;

                        //await program.Login(page);


                        var result = await program.PreviousSearches(page, tempVin);

                        if (!result.Item2)
                        {
                            if (result.Item3.Equals(program.vin))
                            {
                                await program.DistributeSearch(page, tempcurrentReqID);

                                await program.DistributeToEmail(page, email, tempcurrentReqID);
                            }
                            request = newRequest;
                            if (request != "")
                            {
                                program.newRequest = true;
                                request = newRequest;
                            }

                        }
                    }


                }
                if (!prevSearch || program.newRequest)
                {
                    program.vin = request.Substring(request.IndexOf("_") + 1);
                    //var tempcurrentReqID = "";
                    //  tempVin = program.vin;
                    if (program.vin != "")
                    {
                        program.currentReqID = request.Substring(0, request.IndexOf("_"));
                        // tempcurrentReqID = program.currentReqID;
                    }
                    else
                    {
                        return;
                    }

                    Console.WriteLine(program.vin);
                    //await program.Login(page);
                    var result = await program.PreviousSearches(page, program.vin);
                    if (!result.Item2)
                    {
                        if (result.Item3.Equals(program.vin))
                        {
                            await program.DistributeSearch(page, program.currentReqID);

                            await program.DistributeToEmail(page, email, program.currentReqID);

                        }
                        //request = program.start();
                    }
                    else
                    {
                        await program.RegistrySection(page);

                        await program.Search(page, program.vin);

                        await program.ContinueSearch(page);

                        await program.DistributeSearch(page, program.currentReqID);

                        await program.DistributeToEmail(page, email, program.currentReqID);
                    }

                    //old code below

                    ////string str = program.nextVINReq();
                    //program.vin = request.Substring(request.IndexOf("_") + 1);

                    //if (program.vin != "")
                    //{
                    //    program.currentReqID = request.Substring(0, request.IndexOf("_"));
                    //}
                    //else
                    //{
                    //    return;
                    //}
                    //Console.WriteLine(program.vin);
                    ////await program.Login(page);
                    //var result = await program.PreviousSearches(page, program.vin);
                    //if (!result.Item2)
                    //{
                    //    await program.DistributeSearch(page, program.currentReqID);

                    //    await program.DistributeToEmail(page, email, program.currentReqID);


                    //    //request = program.start();
                    //}
                    //else
                    //{
                    //    await program.RegistrySection(page);

                    //    await program.Search(page, program.vin);

                    //    await program.ContinueSearch(page);

                    //    await program.DistributeSearch(page, program.currentReqID);

                    //    await program.DistributeToEmail(page, email, program.currentReqID);
                    //}
                    ////program.start();
                    //program.InActivate();

                    //program.start();
                    if (request != "")
                    {
                        // program.newRequest = true;
                        // program.myServiceHost.Close();
                        //program.InActivate();
                        Console.WriteLine(program.vin);
                        await Task.Delay(1000);

                        // Close the browser
                        //await browser.CloseAsync();
                        //  Main(new string[] { request }).GetAwaiter().GetResult();
                        //Environment.Exit(0);
                    }



                }

            }
            catch (System.Exception exception1)
            {

                Log($"Error: {exception1.Message}");
                //sendEmail(exception.ToString());
                //Log("Service host closed");
                program.InActivate();
                // this.myServiceHost.Close();
                CloseServiceHost();
                //  Environment.Exit(0);
                // Console.ReadLine();

                throw;
            }

            //  Console.ReadLine();
            //program.InActivate();
            //await browser.CloseAsync();
            //Environment.Exit(0);
            //program.myServiceHost.Close();

            program.start();



        }


        private async Task<(IPage, bool)> Login(IPage page)
        {
            try
            {
                await page.DeleteCookieAsync();
                await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                {
                    ["Cache-Control"] = "no-cache",
                    ["Pragma"] = "no-cache"
                });
                // Navigate to the login page
                await page.GoToAsync("https://appres.alberta.ca/GOA.APPRES.Login/Login_Alberta.aspx", new NavigationOptions { Timeout = 30000 });

                await page.ClickAsync("#btnLgnUsingMADI");

                //// Wait for navigation or a specific element to confirm login success
                await page.WaitForNavigationAsync();

                // Check if the button exists before clicking
                //// Wait for the button or input field to be present and visible
                ///


                //await page.WaitForSelectorAsync("goa-button", new WaitForSelectorOptions { Visible = true });
                //var button = await page.QuerySelectorAsync("goa-button"); // Adjust the selector if needed
                //var isSignInButtonEnabled = await page.EvaluateFunctionAsync<bool>("(button) => !button.disabled ", button);

                await page.WaitForSelectorAsync("goa-button", new WaitForSelectorOptions { Visible = true });

                // Get the host <goa-button>
                var buttonHost = await page.QuerySelectorAsync("goa-button");

                // Check the real <button> inside the shadowRoot
                var isSignInButtonEnabled = await page.EvaluateFunctionAsync<bool>(@"(host) => {
    const btn = host.shadowRoot && host.shadowRoot.querySelector('button');
    if (!btn) return false;
    return !btn.disabled && btn.offsetParent !== null;
}", buttonHost);
                if (isSignInButtonEnabled)
                {


                    var newContentSelector = "goa-form-item"; // Replace with the actual selector of the new content
                    await page.WaitForSelectorAsync(newContentSelector, new WaitForSelectorOptions { Visible = true, Timeout = 1000 }); // Adjust timeout as needed


                    var inputElement = await page.WaitForSelectorAsync("goa-input", new WaitForSelectorOptions { Visible = true, Timeout = 1000 }); // Adjust timeout as needed

                    await inputElement.FocusAsync();

                    //await inputElement.TypeAsync("UCDAON"); // Replace
                    await page.Keyboard.TypeAsync("UCDAON", new TypeOptions { Delay = 100 });
                    await Task.Delay(1000);


                    await page.Keyboard.PressAsync("Enter");
                    // wait for either error div or password field to appear
                    await page.WaitForFunctionAsync(@"() => {
    return document.querySelector('div.error-msg') ||
           [...document.querySelectorAll('goa-input')]
             .some(el => el.shadowRoot?.querySelector('input[type=password]'));
}", new WaitForFunctionOptions { Timeout = 10000 });




                    var button2 = await page.QuerySelectorAsync("goa-button");





                    //var isContinueButtonEnabled = await page.EvaluateFunctionAsync<bool>("(button) => !button.disabled && button.offsetParent !== null", button2);
                    //if (isContinueButtonEnabled)
                    //{

                        await page.WaitForSelectorAsync(newContentSelector, new WaitForSelectorOptions { Visible = true, Timeout = 30000 }); // Adjust timeout as needed
                        inputElement = await page.WaitForSelectorAsync("goa-input", new WaitForSelectorOptions { Visible = true, Timeout = 30000 }); // Adjust timeout as needed

                        await inputElement.FocusAsync();
                        await Task.Delay(1000);
                        //await inputElement.TypeAsync("K1llB1ll"); // Replace
                        await page.Keyboard.TypeAsync("K1llB1ll", new TypeOptions { Delay = 100 });
                        await Task.Delay(1000);
                        await page.Keyboard.PressAsync("Enter");

                        await page.WaitForNavigationAsync();
                        await Task.Delay(2000);
                        this.loggedIn = true;
                    //}
                }

            }
            catch (Exception exception1) when (
     exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return (page, loggedIn);
        }







        private async Task<IPage> RegistrySection(IPage page)
        {
            try
            {

                Log("Login Successful");

                await Task.Delay(2000);
                // Navigate to the Registry Page
                await page.GoToAsync("https://appres.alberta.ca/GOA.APPRES.Web/InitiateTransaction.aspx?ServiceTypeID=CAA45F61-80A1-4EEE-9ACD-B02B856678BC", new NavigationOptions { Timeout = 30000 });
                await Task.Delay(3000);
                // Define the src attribute of the iframe you want to access
                var frames = page.Frames;
                // Find the frame by its src attribute
                var iframeSrc = "https://appres.alberta.ca/GOA.APPRES.Web/InitiateTransaction.aspx?ServiceTypeID=CAA45F61-80A1-4EEE-9ACD-B02B856678BC"; // Replace with the actual src of the iframe




                var targetFrame = frames.FirstOrDefault(frame => frame.Url.Contains(iframeSrc));

                if (targetFrame != null)
                {
                    //Check if error occurs

                    // Search for any of the words or sentences inside the iframe
                    string pageContent = await targetFrame.EvaluateFunctionAsync<string>("() => document.body.innerText");
                    Form1 program = new Form1();
                    foreach (var term in program.errorTerms)
                    {
                        if (pageContent.Contains(term))
                        {
                            throw new Exception($"Error found: {term}");
                        }
                    }

                    // Console.WriteLine($"Found the frame with src: {iframeSrc}");

                    // Optionally, you can interact with the frame, e.g., by typing in an input field within the iframe
                    var dropDownSelector = "#SearchRequest_serviceDropDown"; // Replace with the actual selector inside the iframe
                    await targetFrame.WaitForSelectorAsync(dropDownSelector, new WaitForSelectorOptions { Visible = true });

                    // Select the item by value
                    var valueToSelect = "fdaf04e0-6828-4f8a-ae76-6f028150ab4d"; // Replace with the actual value you want to select
                    await page.SelectAsync(dropDownSelector, valueToSelect);

                    Console.WriteLine($"Selected the item with value: {valueToSelect}");

                    // Optionally, you can take further actions like submitting a form or checking the result
                    var submitButtonSelector = "#SearchRequest_GoButton"; // Replace with the actual selector
                    await page.ClickAsync(submitButtonSelector);

                    // Optionally wait for navigation or other actions to complete
                    await page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0 } });
                    await Task.Delay(3000);
                    // Optionally extract content from the new page
                    var newPageContent = await page.GetContentAsync();


                    //Console.WriteLine(newPageContent);
                }
                else
                {
                    Console.WriteLine("Iframe not found.");
                }
            }
            catch (Exception exception1) when (
    exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return page;
        }

        private async Task<IPage> Search(IPage page, string vin)
        {
            try
            {
                // Define the class name to search within
                var className = "bandlight"; // Replace with the actual class name

                // Construct the CSS selector to find the first input inside the class
                var selector = $".{className} input";

                // Wait for the first input inside the class to be present and visible
                await page.WaitForSelectorAsync(selector);

                // Query the first input element inside the class
                var inputElement = await page.QuerySelectorAsync(selector);
                await inputElement.FocusAsync();

                await inputElement.TypeAsync(vin);


                // Define the value of the button you want to click
                var buttonName = "Search"; // Replace with the actual value of the button

                // Construct the CSS selector to find the input by its value attribute
                selector = $"input[name='{buttonName}']";

                // Wait for the button to be present and visible
                await page.WaitForSelectorAsync(selector);

                // Query the button by its value and click it
                var buttonElement = await page.QuerySelectorAsync(selector);


                // Click the button
                await buttonElement.ClickAsync();

                await page.WaitForNavigationAsync();
                await Task.Delay(3000);
            }
            catch (Exception exception1) when (
     exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return page;
        }

        private async Task<IPage> ContinueSearch(IPage page)
        {
            try
            {
                // Define the value of the button you want to click
                var buttonName = "Continue"; // Replace with the actual value of the button

                // Construct the CSS selector to find the input by its value attribute
                var selector = $"input[value='{buttonName}']";

                // Wait for the button to be present and visible
                await page.WaitForSelectorAsync(selector);

                // Query the button by its value and click it
                var buttonElement = await page.QuerySelectorAsync(selector);


                // Click the button
                await buttonElement.ClickAsync();

                await page.WaitForNavigationAsync();
                await Task.Delay(3000);
            }
            catch (Exception exception1) when (
    exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return page;
        }

        private async Task<IPage> DistributeSearch(IPage page, string currentReqID)
        {
            try
            {
                int noOfLiens = 0;

                // Define the id of the checkbox element
                var checkboxId = "chkSpecificSerialNumberOnly"; // Replace with the actual id of the checkbox

                // Construct the CSS selector to find the checkbox by its id
                var selector = $"#{checkboxId}";


                // Wait for the checkbox to be present and visible
                await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Visible = true });


                // Click the checkbox
                await page.ClickAsync(selector);

                // Define the id of the checkbox element
                var resultsId = "ResultGeneral"; // Replace with the actual id of the checkbox

                // Construct the CSS selector to find the checkbox by its id
                selector = $"#{resultsId}";

                // Wait for the checkbox to be present and visible
                await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Visible = true });


                // Extract the text inside the <strong> tag within the <span> with id 'ResultGeneral'
                var strongText = await page.EvaluateFunctionAsync<string>(@"() => {
            const span = document.querySelector('#ResultGeneral');
            if (span) {
                const strongTag = span.querySelector('strong');
                return strongTag ? strongTag.textContent : null;
            }
            return null;
        }");


                // Display the extracted text
                if (strongText.Contains("Both") || strongText.Contains("Exact"))
                {
                    strongText = await page.EvaluateFunctionAsync<string>(@"() => {
            const span = document.querySelector('#ResultExact');
            if (span) {
                const strongTag = span.querySelector('strong');
                return strongTag ? strongTag.textContent : null;
            }
            return null;
        }");
                    // Regular expression to match text between two circular brackets
                    string pattern = @"\(([^)]+)\)";
                    Match match = Regex.Match(strongText, pattern);

                    if (match.Success)
                    {
                        string result = match.Groups[1].Value;
                        noOfLiens = Convert.ToInt32(result);
                        Console.WriteLine($"Text inside brackets: {result}");
                    }
                    else
                    {
                        Console.WriteLine("No text found inside brackets.");
                    }

                }
                else if (strongText.Contains("Inexact"))
                {
                    noOfLiens = 0;
                }
                SQLSetCompleted(currentReqID, noOfLiens);

                // Construct the CSS selector to find the input by its value attribute
                var buttonName = "Distribute";


                selector = $"input[name='{buttonName}']";

                // Wait for the button to be present and visible
                await page.WaitForSelectorAsync(selector);

                // Query the button by its value and click it
                var buttonElement = await page.QuerySelectorAsync(selector);


                // Click the button
                await buttonElement.ClickAsync();

                await page.WaitForNavigationAsync();
                await Task.Delay(3000);
            }
            catch (Exception exception1) when (
     exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return page;
        }

        private async Task<IPage> DistributeToEmail(IPage page, string email, string RId)
        {
            // Optionally extract content from the new page



            var buttonName = "ctrlPDD:PDDAddNew";


            var selector = $"input[name='{buttonName}']";

            // Wait for the button to be present and visible
            await page.WaitForSelectorAsync(selector);

            // Query the button by its value and click it
            var buttonElement = await page.QuerySelectorAsync(selector);


            // Click the button
            await buttonElement.ClickAsync();

            await page.WaitForNavigationAsync();
            // Define the selector for the dropdown (select) element
            var dropdownSelector = "#ctrlPDD_DDLControl"; // Replace with the actual selector for your dropdown



            // Wait for the dropdown to be present in the DOM
            await page.WaitForSelectorAsync(dropdownSelector);

            // Select the value from the dropdown
            var valueToSelect = "Email"; // Replace with the actual value attribute of the option you want to select
            await page.SelectAsync(dropdownSelector, valueToSelect);

            await page.WaitForNavigationAsync();

            var inputName = "ctrlPDD:_txtEmailTo"; // Replace with the actual class name

            // Construct the CSS selector to find the first input inside the class
            selector = $"input[name='{inputName}']";

            // Wait for the first input inside the class to be present and visible
            await page.WaitForSelectorAsync(selector);

            // Query the first input element inside the class
            var inputElement = await page.QuerySelectorAsync(selector);
            await inputElement.FocusAsync();

            await inputElement.TypeAsync(email);

            // Wait for the dropdown to be present in the DOM
            await page.WaitForSelectorAsync(dropdownSelector);

            inputName = "ctrlPDD:_txtEmailSubject"; // Replace with the actual class name

            // Construct the CSS selector to find the first input inside the class
            selector = $"input[name='{inputName}']";

            // Wait for the first input inside the class to be present and visible
            await page.WaitForSelectorAsync(selector);

            // Query the first input element inside the class
            inputElement = await page.QuerySelectorAsync(selector);
            await inputElement.FocusAsync();

            await inputElement.TypeAsync(RId);

            // Query the button by its value and click it
            // buttonElement = await page.QuerySelectorAsync(selector);


            //// Click the button
            //await buttonElement.ClickAsync();

            //await page.WaitForNavigationAsync();
            //await Task.Delay(3000);
            buttonName = "ctrlPDD_btnSavePPD";


            selector = $"input[id='{buttonName}']";

            // Wait for the button to be present and visible
            await page.WaitForSelectorAsync(selector);

            // Query the button by its value and click it
            buttonElement = await page.QuerySelectorAsync(selector);


            // Click the button
            await buttonElement.ClickAsync();

            await page.WaitForNavigationAsync();

            // Define the value of the button you want to click
            buttonName = "Continue"; // Replace with the actual value of the button

            // Construct the CSS selector to find the input by its value attribute
            selector = $"input[value='{buttonName}']";

            // Wait for the button to be present and visible
            await page.WaitForSelectorAsync(selector);

            // Query the button by its value and click it
            buttonElement = await page.QuerySelectorAsync(selector);


            // Click the button
            await buttonElement.ClickAsync();

            await page.WaitForNavigationAsync();
            await Task.Delay(3000);

            var newPageContent = await page.GetContentAsync();


            return page;
        }

        private async Task<(IPage, bool, string)> PreviousSearches(IPage page, string vin)
        {
            try
            {
                bool result = false;
                await Task.Delay(2000);
                // Navigate to the Registry Page
                await page.GoToAsync("https://appres.alberta.ca/GOA.APPRES.Web/InitiateTransaction.aspx?ServiceTypeID=CAA45F61-80A1-4EEE-9ACD-B02B856678BC");
                await Task.Delay(3000);
                // Define the src attribute of the iframe you want to access
                var frames = page.Frames;
                // Find the frame by its src attribute
                var iframeSrc = "https://appres.alberta.ca/GOA.APPRES.Web/InitiateTransaction.aspx?ServiceTypeID=CAA45F61-80A1-4EEE-9ACD-B02B856678BC"; // Replace with the actual src of the iframe




                var targetFrame = frames.FirstOrDefault(frame => frame.Url.Contains(iframeSrc));

                if (targetFrame != null)
                {
                    //Console.WriteLine($"Found the frame with src: {iframeSrc}");


                    // Optionally, you can take further actions like submitting a form or checking the result
                    var submitButtonSelector = "#WcBrowsePerformedSearches_goButton"; // Replace with the actual selector
                    await page.ClickAsync(submitButtonSelector);

                    // Optionally wait for navigation or other actions to complete
                    await page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0 } });
                    await Task.Delay(3000);
                    // Optionally extract content from the new page

                    // find undistributed search and click on launch button

                    // Wait for the table row to be visible
                    await page.WaitForSelectorAsync("tr");
                    // Use querySelectorAll to get all rows in the table
                    var rows = await page.QuerySelectorAllAsync("table tr");
                    // Define the class name and the value you are looking for
                    string targetClassName = "bandlight";
                    string targetValue = vin;
                    // Iterate over each row and check the value in the 8th column
                    foreach (var row in rows)
                    {
                        // Select the 8th column <td> and get its text content
                        var cell = await row.QuerySelectorAsync("td:nth-child(8)");
                        if (cell != null)
                        {
                            var cellText = await page.EvaluateFunctionAsync<string>("el => el.textContent.trim()", cell);

                            // If the cell matches the value we're looking for
                            if (cellText == targetValue)
                            {
                                // Find the corresponding button in the last column and click it
                                var button = await row.QuerySelectorAsync("td:last-child input[type='submit']");
                                if (button != null)
                                {
                                    await button.ClickAsync();
                                    Console.WriteLine("Distribution Button clicked successfully.");
                                    result = true;
                                    this.newRequest = false;
                                    //break; // Exit the loop after clicking the button
                                }
                                return (page, this.newRequest, vin);
                            }
                        }
                    }

                    if (result)
                    {
                        Console.WriteLine("Match found!");
                    }
                    else
                    {
                        Console.WriteLine("No match found in previous searches.");
                        this.newRequest = true;
                        return (page, this.newRequest, vin);
                    }

                }
                else
                {
                    Console.WriteLine("Iframe not found.");
                }
            }
            catch (Exception exception1) when (
    exception1 is TimeoutException ||
    exception1 is PuppeteerSharp.WaitTaskTimeoutException ||
    exception1 is PuppeteerSharp.NavigationException && exception1.Message.Contains("net::ERR_TOO_MANY_REDIRECTS") ||
    exception1 is NullReferenceException ||
    exception1.Message.Contains("Execution context was destroyed") ||
    exception1.Message.Contains("Node is either not visible or not an HTMLElement") ||
    exception1.Message.Contains("Node could not be found for selector") ||
    exception1.Message.Contains("No node found for selector"))
            {
                Log($"Error: {exception1.Message}");
                this.InActivate();
                CloseServiceHost();
                start();

                // Optional: log or display error
                // LogError(exception1); // or MessageBox.Show(exception1.Message);
            }
            return (page, this.newRequest, vin);
        }
        //Old code reuse below

        //protected string Load()
        //{



        //    string str = "";
        //    string[] strArrays = this.appName.Split(new char[] { '/' });
        //    string[] strArrays1 = strArrays[2].Split(new char[] { ':' });
        //    this.host = strArrays1[0];
        //    port = Convert.ToInt16(strArrays1[1]);
        //    this.app = strArrays[3];
        //    this.Activate();
        //    Uri uri = new Uri(this.appName);
        //    try
        //    {
        //        this.myServiceHost = new ServiceHost(typeof(PatientService), new Uri[] { uri });


        //        // Check if ServiceHost is opened and working
        //        if (this.myServiceHost.State == CommunicationState.Opened)
        //        {
        //            Console.WriteLine("ServiceHost is working and accepting requests.");
        //        }
        //        //else if(this.myServiceHost.State == CommunicationState.Created)
        //        //{
        //        //    Console.WriteLine("ServiceHost is already created and accepting requests.");
        //        //}
        //        else
        //        {

        //            this.myServiceHost.Open();
        //        }

        //        //this.myServiceHost.Close();
        //    }
        //    catch (System.Exception exception1)
        //    {
        //        System.Exception exception = exception1;
        //        sendEmail(exception.ToString());
        //        Console.WriteLine(exception.ToString());
        //        this.InActivate();
        //        throw;
        //    }

        //    string str1 = this.ReportCompletedSearchesNotDistributed();
        //    if (str1 == "")
        //    {

        //        if (this.searchOpen())
        //        {
        //            str = start();
        //        }
        //    }
        //    else
        //    {
        //        try
        //        {
        //            markBusy();
        //            //this.vin = str1.Substring(str1.IndexOf("_") + 1);
        //            //this.currentReqID = str1.Substring(0, str1.IndexOf("_"));
        //            this.prevSearch = true;
        //            str = str1;
        //        }
        //        catch (System.Exception exception2)
        //        {
        //            sendEmail(exception2.ToString());
        //            this.InActivate();
        //            throw;
        //        }
        //    }
        //    return str;
        //}



        protected async void start()
        {
            ResetIdleTimer();

            //if (this.myServiceHost.State == CommunicationState.Opened)
            //{
            //    Console.WriteLine("ServiceHost is still working and accepting requests.");

            //}
            // this.ReportCompletedSearchesNotDistributed();
            this.label1.Text = DateTime.Now.ToString();
            //  string temp = strPrev;
            strPrev = this.ReportCompletedSearchesNotDistributed();
            string temp = strPrev.Substring(strPrev.IndexOf("_") + 1);
            string str = this.nextVINReq();

            // str = "12345_1L9GA72A2GL033207";

            if (str == "" && temp.Equals(vin))
            {
                this.markAvailable();
                // await ProcessRequest("", false);

            }
            else if (str != "")
            {
                this.markBusy();
                //this.vin = str.Substring(str.IndexOf("_") + 1);
                //this.currentReqID = str.Substring(0, str.IndexOf("_"));
                // this.newRequest = true;
                // Main(new string[] { }).GetAwaiter().GetResult();
                //sendEmail("Listened to a request");
                await ProcessRequest(str, false);
                ResetIdleTimer();
            }

            else if (strPrev != "" && !strPrev.Equals(vin))
            {
                this.markBusy();
                //this.vin = str.Substring(str.IndexOf("_") + 1);
                //this.currentReqID = str.Substring(0, str.IndexOf("_"));
                //  this.prevSearch = true;
                // Main(new string[] { }).GetAwaiter().GetResult();
                //sendEmail("Listened to a request");

                await ProcessRequest(strPrev, true);
                ResetIdleTimer();
            }

            else
            {
                try
                {
                    this.markBusy();
                    //this.vin = str.Substring(str.IndexOf("_") + 1);
                    //this.currentReqID = str.Substring(0, str.IndexOf("_"));
                    // this.newRequest = true;
                    // Main(new string[] { }).GetAwaiter().GetResult();
                    //sendEmail("Listened to a request");
                    await ProcessRequest(str, false);
                    ResetIdleTimer();
                }
                catch (System.Exception exception1)
                {
                    // sendEmail(exception.ToString());
                    Log($"Error: {exception1.Message}");
                    this.InActivate();
                    this.myServiceHost.Close();
                    //  throw;
                }
            }
            //return str;
        }
        /// <summary>
        /// Activate Method
        /// </summary>
        protected void Activate()
        {
            try
            {
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str = string.Format("if ((Select count(*) From dbo.RegisteredApps where Host='{0}' and Port={1} and App='{2}') = 0) Begin Insert into dbo.RegisteredApps (Host,Port, App) values ('{0}',{1},'{2}') End", this.host, port, this.app);
                sqlConnection.Open();
                (new SqlCommand(str, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                //sendEmail(exception.ToString());
                listBox1.Items.Add($"Exception: {exception.Message}");
                this.InActivate();
                // Log("Service host closed");
                this.myServiceHost.Close();
                // throw;
            }
        }

        /// <summary>
        /// Inactivate Method
        /// </summary>
        protected void InActivate()
        {
            Log("Inactivating registered app");
            string machineName = Environment.MachineName;
            try
            {

                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str = string.Format("Delete From dbo.RegisteredApps Where Host = '{0}'and Port = {1} and App = '{2}'", this.host, 2207, this.app);
                sqlConnection.Open();
                (new SqlCommand(str, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();

            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                //sendEmail(exception.ToString());
                //Log("Service host closed");
                this.InActivate();
                this.myServiceHost.Close();

                // throw;
            }

        }
        /// <summary>
        /// Unditributed Searches
        /// 
        /// </summary>
        /// <returns></returns>

        protected static void sendEmail(string subj)
        {
            //string[] strArrays = Settings.Default.smtpTo.ToString().Split(new char[] { ';' });
            string[] strArrays = { "k.alagh@ucda.org" };
            MailMessage mailMessage = new MailMessage();
            for (int i = 0; i < (int)strArrays.Length; i++)
            {
                mailMessage.To.Add(strArrays[i]);
            }
            mailMessage.Subject = "Error in LNAB";
            mailMessage.Body = string.Concat(new string[] { "Application: ", "\n\nException: ", subj, "\n" });
            try
            {
                (new SmtpClient()).Send(mailMessage);
            }
            catch (SmtpException smtpException1)
            {
                SmtpException smtpException = smtpException1;
                // this.label2.Text = string.Concat(this.label2.Text, "\n", smtpException.ToString().Substring(0, 120));
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                //this.label2.Text = string.Concat(this.label2.Text, "\n", exception.ToString().Substring(0, 120));
                //this.myServiceHost.Close();
                throw;
            }
        }

        protected void markAvailable()
        {
            try
            {
                string[] strArrays = this.appName.Split(new char[] { '/' });
                string[] strArrays1 = strArrays[2].Split(new char[] { ':' });
                this.host = strArrays1[0];
                port = Convert.ToInt16(strArrays1[1]);
                this.app = strArrays[3];
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str = string.Format("Update dbo.RegisteredApps Set StartTime=null Where Host = '{0}'and Port = {1} and App = '{2}'", new object[] { this.host, 2207, this.app, null });
                sqlConnection.Open();
                (new SqlCommand(str, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                // sendEmail(exception.ToString());
                this.InActivate();
                this.myServiceHost.Close();
                //throw;
            }
        }

        protected void markBusy()
        {
            try
            {
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                DateTime now = DateTime.Now;
                string[] str = new string[7];
                int year = now.Year;
                str[0] = year.ToString();
                str[1] = "-";
                year = now.Month;
                str[2] = year.ToString();
                str[3] = "-";
                year = now.Day;
                str[4] = year.ToString();
                str[5] = " ";
                str[6] = now.TimeOfDay.ToString();
                string str1 = string.Concat(str);
                string str2 = string.Format("Update dbo.RegisteredApps Set StartTime='{0}' Where Host = '{1}'and Port = {2} and App = '{3}'", new object[] { str1, this.host, port, this.app });
                sqlConnection.Open();
                (new SqlCommand(str2, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                // sendEmail(exception.ToString());
                this.InActivate();
                // Log("Service host closed");
                this.myServiceHost.Close();
                // throw;
            }
        }

        private string nextVINReq()
        {
            string str = "";
            string str1 = this.ReportUnCompletedSearches();
            if (str1 != "")
            {
                str = str1.ToString();
                this.reserve(str);
            }
            return str;
        }

        private string ReportUnCompletedSearches()
        {
            string str = "";
            try
            {
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str1 = string.Format("SELECT Top 1 A.RequestID, B.VIN From OOPRequests A inner join Requests B on A.RequestID=B.RequestID WHERE A.Completed=0 and A.DroneName IS NULL and Province = 'AB'", new object[0]);
                sqlConnection.Open();
                SqlDataReader sqlDataReader = (new SqlCommand(str1, sqlConnection)).ExecuteReader();
                while (sqlDataReader.Read())
                {
                    str = string.Concat(sqlDataReader["RequestID"].ToString().Trim(), "_", sqlDataReader["VIN"].ToString().Trim());
                }
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                //  sendEmail(exception.ToString());
                this.InActivate();
                //Log("Service host closed");
                this.myServiceHost.Close();
                //throw;
            }
            return str;
        }
        private string ReportCompletedSearchesNotDistributed()
        {
            string str = "";
            try
            {
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str1 = string.Format("SELECT Top 1 A.RequestID, B.VIN From OOPRequests A inner join Requests B on A.RequestID=B.RequestID WHERE A.Completed=1 and A.DroneName IS NOT NULL and Province = 'AB'", new object[0]);
                sqlConnection.Open();
                SqlDataReader sqlDataReader = (new SqlCommand(str1, sqlConnection)).ExecuteReader();
                while (sqlDataReader.Read())
                {
                    str = string.Concat(sqlDataReader["RequestID"].ToString().Trim(), "_", sqlDataReader["VIN"].ToString().Trim());
                }
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                //  sendEmail(exception.ToString());
                this.InActivate();
                //  Log("Service host closed");
                this.myServiceHost.Close();
                // throw;
            }
            return str;
        }

        protected void reserve(string uncompl)
        {
            try
            {
                int num = Convert.ToInt32(uncompl.Substring(0, uncompl.IndexOf("_")));
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                string str = string.Format("Update OOPRequests Set DroneName='{0}', Completed = 1 Where  RequestID = {1} and Province = 'AB' And DroneName  IS NULL", this.machineName, num);
                sqlConnection.Open();
                (new SqlCommand(str, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                //sendEmail(exception.ToString());
                this.InActivate();
                // Log("Service host closed");
                this.myServiceHost.Close();
                // throw;
            }
        }

        protected void SQLSetCompleted(string currentReqID, int noOfLiens)
        {
            try
            {
                SqlConnection sqlConnection = new SqlConnection(this.conn);
                DateTime now = DateTime.Now;
                string str = string.Format("Update OOPRequests Set EndTime='{0}', SubmittedTimes= {1} Where  RequestID = {2} and Province = 'AB'", now, noOfLiens, currentReqID);
                sqlConnection.Open();
                (new SqlCommand(str, sqlConnection)).ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (System.Exception exception1)
            {
                System.Exception exception = exception1;
                listBox1.Items.Add($"Exception: {exception.Message}");
                this.InActivate();
                //Log("Service host closed");
                this.myServiceHost.Close();
                //throw;
            }
        }

        protected bool searchOpen()
        {
            bool flag = false;
            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            if (dayOfWeek < 6 && dayOfWeek > 0)
            {
                TimeSpan timeSpan = new TimeSpan(8, 35, 0);
                TimeSpan timeSpan1 = new TimeSpan(23, 10, 0);
                TimeSpan timeOfDay = DateTime.Now.TimeOfDay;
                if (timeOfDay > timeSpan && timeOfDay < timeSpan1)
                {
                    flag = true;
                }
            }
            else if (dayOfWeek != 6)
            {
                TimeSpan timeSpan2 = new TimeSpan(14, 5, 0);
                TimeSpan timeSpan3 = new TimeSpan(20, 0, 0);
                TimeSpan timeOfDay1 = DateTime.Now.TimeOfDay;
                if (timeOfDay1 > timeSpan2 && timeOfDay1 < timeSpan3)
                {
                    flag = true;
                }
            }
            else
            {
                TimeSpan timeSpan4 = new TimeSpan(8, 35, 0);
                TimeSpan timeSpan5 = new TimeSpan(20, 10, 0);
                TimeSpan timeOfDay2 = DateTime.Now.TimeOfDay;
                if (timeOfDay2 > timeSpan4 && timeOfDay2 < timeSpan5)
                {
                    flag = true;
                }
            }
            return flag;
        }

        public static void reStart()
        {
            Form1 form1 = new Form1();

            form1.InActivate();

            if (browser != null)
            {
                browser.CloseAsync();
            }
            string exePath = Assembly.GetExecutingAssembly().Location;

            // Start a new instance of the application
            Process.Start(exePath);

            // Terminate the current application
            Environment.Exit(0);
        }

        public static async void preStart()
        {

            Form1 form1 = new Form1();

            // Log("Received new request");
            //new Program().start();
            //string str = "";
            //string[] strArrays = this.appName.Split(new char[] { '/' });
            //string[] strArrays1 = strArrays[2].Split(new char[] { ':' });
            //this.host = strArrays1[0];
            //port = Convert.ToInt16(strArrays1[1]);
            //this.app = strArrays[3];
            //this.Activate();
            //Uri uri = new Uri(this.appName);
            //this.myServiceHost = new ServiceHost(typeof(PatientService), new Uri[] { uri });


            //// Check if ServiceHost is opened and working
            //if (this.myServiceHost.State == CommunicationState.Opened)
            //{
            //    Console.WriteLine("ServiceHost is working and accepting requests.");
            //}
            //else if (this.myServiceHost.State == CommunicationState.Created)
            //{
            //    Console.WriteLine("ServiceHost is already created and accepting requests.");

            //}
            //else
            //{
            //    this.myServiceHost.Open();

            //}
            try
            {
                ////this.InActivate();
                //// new Program().myServiceHost.Close();
                //if (this.myServiceHost != null && this.myServiceHost.State == CommunicationState.Opened)
                //{
                //    this.myServiceHost.Close();
                //    Console.WriteLine("ServiceHost is closed to avoid conflicts.");
                //}
                ////
                // Close the browser

                // browser.CloseAsync();
                //CloseServiceHost();
                new Form1().start();

                //ProcessRequest(request,false);
                // Main(new string[] { request }).GetAwaiter().GetResult();
                //Environment.Exit(0);
            }
            catch (Exception ex)
            {

                var a = ex.ToString();
            }
        }
        public void OnProcessExit(object sender, EventArgs e)
        {
            Log("Exiting program");
            // new Program().myServiceHost.Close();
            // This is where you can clean up resources or log information before the application closes
            ShutdownBrowser();
            this.InActivate();
            this.myServiceHost.Close();
        }

        public static void ShutdownBrowser()
        {
            try
            {
                if (page != null && !page.IsClosed)
                {
                    page.CloseAsync().GetAwaiter().GetResult();
                }
            }
            catch { }
            finally
            {
                page = null;
            }

            try
            {
                if (browser != null)
                {
                    if (browser.IsConnected)
                    {
                        browser.CloseAsync().GetAwaiter().GetResult();
                    }
                    browser.Process?.Kill();
                }
            }
            catch { }
            finally
            {
                browser = null;
            }
        }

        public static void CloseServiceHost()
        {
            Form1 program = new Form1();
            if (program.myServiceHost != null)
            {
                try
                {
                    // Gracefully close the service host
                    Log("Service host closed");
                    program.myServiceHost.Close();
                    Console.WriteLine("ServiceHost closed gracefully.");
                }
                catch (CommunicationObjectFaultedException)
                {
                    // Forcefully abort the service host if close fails
                    program.myServiceHost.Abort();
                    Console.WriteLine("ServiceHost aborted.");
                }
                finally
                {
                    // Dispose the service host
                    program.myServiceHost = null;
                    Console.WriteLine("ServiceHost disposed.");
                }
            }
        }

        static void Log(string message,
       [CallerLineNumber] int lineNumber = 0,
       [CallerMemberName] string memberName = "",
       [CallerFilePath] string filePath = "")
        {
            Console.WriteLine($"[{memberName} - Line {lineNumber}] {message}");
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        public void Form1_Closing(object sender, CancelEventArgs e)
        {
            //listener.Stop();
            //listener.Close();
            try
            {
                this.InActivate();
                this.myServiceHost.Close();
                ShutdownBrowser();
                base.Closing -= new CancelEventHandler(this.Form1_Closing);
            }
            catch { }
            finally
            {
                Application.Exit();
            }

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void btnProcessRequest_Click_1(object sender, EventArgs e)
        {
            preStart();
        }

        private void Log(string message)
        {
            if (listBox1.InvokeRequired)
            {
                listBox1.Invoke(new Action(() => Log(message)));
            }
            else
            {
                string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
                listBox1.Items.Add(timestampedMessage);

                // Optional: auto-scroll to the latest entry
                listBox1.TopIndex = listBox1.Items.Count - 1;
            }

            LogToFile(message); // Optional
        }

        private void LogToFile(string message)
        {
            string logFile = Path.Combine(Application.StartupPath, "log.txt");
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(logFile, timestampedMessage + Environment.NewLine);
        }

        private static (TimeSpan start, TimeSpan stop) GetWindow(SupplierSchedule s, DayOfWeek d) =>
    d == DayOfWeek.Sunday ? (s.SunStart, s.SunStop) :
    d == DayOfWeek.Saturday ? (s.SatStart, s.SatStop) :
                              (s.WeekStart, s.WeekStop); // Mon–Fri

        // [start, stop) – include start, exclude stop; supports overnight ranges (e.g., 22:00–06:00)
        private static bool InWindow((TimeSpan start, TimeSpan stop) w, TimeSpan now) =>
            (w.start == TimeSpan.Zero && w.stop == TimeSpan.Zero) ? false
          : (w.start <= w.stop ? (now >= w.start && now < w.stop)
                               : (now >= w.start || now < w.stop));

        private static DateTime CurrentStop((TimeSpan start, TimeSpan stop) w, DateTime now) =>
            (w.start <= w.stop) ? now.Date + w.stop
                                : (now.TimeOfDay >= w.start ? now.Date.AddDays(1) + w.stop
                                                            : now.Date + w.stop);

        private static DateTime NextStart(SupplierSchedule s, DateTime now)
        {
            var today = GetWindow(s, now.DayOfWeek);
            var inside = InWindow(today, now.TimeOfDay);
            if (!inside && !(today.start == TimeSpan.Zero && today.stop == TimeSpan.Zero))
            {
                if (today.start <= today.stop)
                {
                    if (now.TimeOfDay < today.start) return now.Date + today.start;
                }
                else
                {
                    if (now.TimeOfDay < today.start) return now.Date + today.start;
                }
            }
            for (int i = 1; i <= 7; i++)
            {
                var d = now.Date.AddDays(i);
                var w = GetWindow(s, d.DayOfWeek);
                if (w.start == TimeSpan.Zero && w.stop == TimeSpan.Zero) continue;
                return d + w.start;
            }
            return now.AddDays(1);
        }

        // fires at each boundary; flips state and re-arms itself
        private void BoundaryTimer_Tick(object sender, EventArgs e)
        {
            _boundaryTimer.Stop();
            _boundaryTimer.Tick -= BoundaryTimer_Tick;

            var now = DateTime.Now;
            var w = GetWindow(_sched, now.DayOfWeek);
            var inside = InWindow(w, now.TimeOfDay);

            if (inside)
            {
                // just entered active window
                if (this.searchOpen()) start();
            }
            else
            {
                // just left active window
                this.InActivate();
            }

            // schedule the next boundary
            ArmBoundaryTimer();
        }

        private void ArmBoundaryTimer()
        {
            if (_sched == null) return;

            var now = DateTime.Now;
            var w = GetWindow(_sched, now.DayOfWeek);
            var inside = InWindow(w, now.TimeOfDay);

            // Next boundary: stop if inside, next start if outside
            var nextBoundary = inside ? CurrentStop(w, now) : NextStart(_sched, now);
            var due = nextBoundary - now;
            if (due < TimeSpan.FromMilliseconds(250)) due = TimeSpan.FromMilliseconds(250);

            _boundaryTimer.Stop();
            _boundaryTimer.Tick -= BoundaryTimer_Tick;
            _boundaryTimer.Interval = (int)due.TotalMilliseconds;
            _boundaryTimer.Tick += BoundaryTimer_Tick;
            _boundaryTimer.Start();
        }

    }
}

