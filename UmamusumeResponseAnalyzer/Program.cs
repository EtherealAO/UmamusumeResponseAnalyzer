﻿using Microsoft.Win32;
using Spectre.Console;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UmamusumeResponseAnalyzer.Game;
using UmamusumeResponseAnalyzer.Localization;

namespace UmamusumeResponseAnalyzer
{
    public static class UmamusumeResponseAnalyzer
    {
        static bool runInCmder = false;
        public static async Task Main(string[] args)
        {
            ConsoleHelper.DisableQuickEditMode();
            Console.Title = $"UmamusumeResponseAnalyzer v{Assembly.GetExecutingAssembly().GetName().Version}";
            Console.OutputEncoding = Encoding.UTF8;
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_DISABLEIPV6", "true");
            //加载设置
            Config.Initialize();
            await ParseArguments(args);

            if (!AnsiConsole.Profile.Capabilities.Ansi && !runInCmder)
            { //不支持ANSI Escape Sequences，用Cmder打开
                var cmderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmamusumeResponseAnalyzer", "cmder");
                if (!Directory.Exists(cmderPath))
                    await ResourceUpdater.DownloadCmder(cmderPath);
                using var Proc = new Process();
                var StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(cmderPath, "Cmder.exe"),
                    Arguments = $"/start \"{Environment.CurrentDirectory}\" /task URA",
                    CreateNoWindow = false,
                    UseShellExecute = true
                };
                Proc.StartInfo = StartInfo;
                Proc.Start();
                Proc.WaitForExit();
                Environment.Exit(0);
            }

            string prompt;
            do
            {
                prompt = await ShowMenu();
            }
            while (prompt != Resource.LaunchMenu_Start); //如果不是启动则重新显示主菜单

            Database.Initialize(); //初始化马娘相关数据
            Server.Start(); //启动HTTP服务器

            if (Config.Get(Resource.ConfigSet_EnableNetFilter))
                await NetFilter.Enable();
            //如果存在DMM的token文件则启用直接登录功能
            if (File.Exists(DMM.DMM_CONFIG_FILEPATH) && Config.Get(Resource.ConfigSet_DMMLaunch) && DMM.Accounts.Any())
            {
                if (DMM.Accounts.Count == 1)
                {
                    DMM.Accounts[0].RunUmamusume();
                }
                else
                {
                    prompt = AnsiConsole.Prompt(new SelectionPrompt<string>()
                        .Title("发现多个帐号，请选择要启动的那个")
                        .AddChoices(DMM.Accounts.Select(x => x.Name))
                        .AddChoices(new[] { "启动全部" })
                        .AddChoices(new[] { "取消" }));
                    if (prompt == "启动全部")
                    {
                        DMM.IgnoreExistProcess = true;
                        foreach (var account in DMM.Accounts)
                            account.RunUmamusume();
                    }
                    else if (prompt == "取消")
                    {
                    }
                    else
                    {
                        DMM.Accounts.Find(x => x.Name == prompt)?.RunUmamusume();
                    }
                }
            }

            if (!Server.IsRunning)
            {
                AnsiConsole.WriteLine("URA启动失败，按回车键退出程序");
                Console.ReadLine();
            }
            else
            {
                AnsiConsole.MarkupLine(Resource.LaunchMenu_Start_Started);
                var _closingEvent = new AutoResetEvent(false);
                Console.CancelKeyPress += ((s, a) =>
                {
                    _closingEvent.Set();
                });
                _closingEvent.WaitOne();
            }
        }
        static async Task<string> ShowMenu()
        {
            var selections = new SelectionPrompt<string>()
                .Title(Resource.LaunchMenu)
                .AddChoices(new[]
                {
                    Resource.LaunchMenu_Start,
                    Resource.LaunchMenu_Options,
                    Resource.LaunchMenu_UpdateAssets,
                    Resource.LaunchMenu_UpdateProgram
                }
                );
            // Windows限定功能，其他平台不显示
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (File.Exists($"{Environment.SystemDirectory}\\drivers\\netfilter2.sys"))
                {
                    selections.AddChoice(Resource.LaunchMenu_SetNetfilterTarget);
                    selections.AddChoice(Resource.LaunchMenu_UninstallNetFilterDriver);
                }
                else
                {
                    selections.AddChoice(Resource.LaunchMenu_InstallNetFilterDriver);
                }
                if (UraCoreHelper.GamePaths.Any())
                    selections.AddChoice(Resource.LaunchMenu_InstallUraCore);
            }
            var prompt = AnsiConsole.Prompt(selections);
            if (prompt == Resource.LaunchMenu_Options)
            {
                var multiSelection = new MultiSelectionPrompt<string>()
                    .Title(Resource.LaunchMenu_Options)
                    .Mode(SelectionMode.Leaf)
                    .PageSize(20)
                    .InstructionsText(Resource.LaunchMenu_Options_Instruction);

                // 根据预设值添加选项
                foreach (var i in Config.ConfigSet)
                {
                    multiSelection.AddChoiceGroup(i.Key, i.Value.Where(x => x.Visiable).Select(x => x.Key));
                }
                // 复原配置文件的选择情况
                foreach (var i in Config.Configuration)
                {
                    if (i.Value.Value.GetType() == typeof(bool) && (bool)i.Value.Value)
                    {
                        multiSelection.Select(i.Key);
                    }
                }
                // 如果配置文件中的某一组全被选中，则也选中对应的组
                foreach (var i in Config.ConfigSet)
                {
                    var visiableItems = Config.Configuration.Where(x => x.Value.Visiable);
                    var visiableConfig = i.Value.Where(x => x.Visiable);
                    if (i.Value.Any() && visiableItems.Where(x => x.Value.Value.GetType() == typeof(bool) && (bool)x.Value.Value).Select(x => x.Key).Intersect(i.Value.Select(x => x.Key)).Count() == visiableConfig.Count())
                    {
                        multiSelection.Select(i.Key);
                    }
                }

                var options = AnsiConsole.Prompt(multiSelection);

                foreach (var i in Config.Configuration.Keys)
                {
                    if (Config.Get<object>(i).GetType() != typeof(bool)) continue;
                    if (options.Contains(i))
                        Config.Set(i, true);
                    else
                        Config.Set(i, false);
                }
                Config.Save();
            }
            else if (prompt == Resource.LaunchMenu_UpdateAssets)
            {
                await ResourceUpdater.UpdateAssets();
            }
            else if (prompt == Resource.LaunchMenu_UpdateProgram)
            {
                await ResourceUpdater.UpdateProgram();
            }
            else if (prompt == Resource.LaunchMenu_InstallNetFilterDriver)
            {
                var applicationDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmamusumeResponseAnalyzer");
                var nfapiPath = Path.Combine(applicationDir, "nfapi.dll");
                var nfdriverPath = Path.Combine(applicationDir, "nfdriver.sys");
                var redirectorPath = Path.Combine(applicationDir, "Redirector.dll");
                await ResourceUpdater.DownloadNetFilter(nfapiPath, nfdriverPath, redirectorPath);
                using var Proc = new Process();
                var StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = "--install-netfilter-driver",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Proc.StartInfo = StartInfo;
                Proc.Start();
                Proc.WaitForExit();
                if (File.Exists(nfapiPath) && File.Exists(nfdriverPath) && File.Exists(redirectorPath) && File.Exists($"{Environment.SystemDirectory}\\drivers\\netfilter2.sys"))
                    Config.Set(Resource.ConfigSet_EnableNetFilter, true);
            }
            else if (prompt == Resource.LaunchMenu_UninstallNetFilterDriver)
            {
                using var Proc = new Process();
                var StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = "--uninstall-netfilter-driver",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Proc.StartInfo = StartInfo;
                Proc.Start();
                Proc.WaitForExit();
                Config.Set(Resource.ConfigSet_EnableNetFilter, false);
            }
            else if (prompt == Resource.LaunchMenu_SetNetfilterTarget)
            {
                string host;
                string port;
                string username;
                string password;
                do
                {
                    host = AnsiConsole.Prompt(new TextPrompt<string>(Resource.LaunchMenu_SetNetfilterTarget_AskHost).AllowEmpty());
                    if (Uri.CheckHostName(host) == UriHostNameType.Dns)
                        host = Dns.GetHostAddresses(host)[0].ToString();
                    if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
                } while (!IPAddress.TryParse(host, out var _));
                do
                {
                    port = AnsiConsole.Prompt(new TextPrompt<string>(Resource.LaunchMenu_SetNetfilterTarget_AskPort).AllowEmpty());
                    if (string.IsNullOrEmpty(port)) port = "1080";
                } while (!int.TryParse(port, out var _));
                Config.Set("加速服务器地址", host);
                Config.Set("加速服务器端口", port);

                var proxyType = string.Empty;
                do
                {
                    proxyType = AnsiConsole.Ask<string>("代理服务器类型是s(ocks)/h(ttp)").ToLower();
                } while (proxyType[0] != 's' && proxyType[0] != 'h');
                Config.Set("加速服务器类型", proxyType[0] == 'h' ? "http" : "socks");

                if (proxyType[0] == 's' && AnsiConsole.Confirm(Resource.LaunchMenu_SetNetfilterTarget_AskAuth, false))
                {
                    do
                    {
                        username = AnsiConsole.Ask<string>(Resource.LaunchMenu_SetNetfilterTarget_AskAuthUsername);
                    } while (string.IsNullOrEmpty(username));
                    do
                    {
                        password = AnsiConsole.Prompt(new TextPrompt<string>(Resource.LaunchMenu_SetNetfilterTarget_AskAuthPassword).Secret());
                    } while (string.IsNullOrEmpty(password));
                    Config.Set("加速服务器用户名", username);
                    Config.Set("加速服务器密码", password);
                }
                else
                {
                    Config.Set("加速服务器用户名", string.Empty);
                    Config.Set("加速服务器密码", string.Empty);
                }
                Config.Save();
            }
            else if (prompt == Resource.LaunchMenu_InstallUraCore)
            {
#pragma warning disable CA1416
                AnsiConsole.Clear();
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UmamusumeResponseAnalyzer.ura-core.dll");
                if (stream == null)
                {
                    AnsiConsole.WriteLine("提取内置资源失败，请尝试手动安装");
                    Console.ReadKey();
                    AnsiConsole.Clear();
                    return prompt;
                }
                AnsiConsole.WriteLine($"找到了{UraCoreHelper.GamePaths.Count}个可能的目录");

                foreach (var i in UraCoreHelper.GamePaths)
                {
                    var modulePath = Path.Combine(i, "version.dll");
                    var compatiableModulePath = Path.Combine(i, "winhttp.dll");
                    AnsiConsole.WriteLine($"发现有效的游戏目录: {i}");
                    if (!File.Exists(modulePath) && !File.Exists(compatiableModulePath))
                    {
                        AnsiConsole.WriteLine("未找到任何模块");
                        if (AnsiConsole.Confirm("是否要在此处安装?"))
                        {
                            InstallModule(modulePath);
                        }
                    }
                    else if (File.Exists(compatiableModulePath))
                    {
                        AnsiConsole.WriteLine("发现其他以兼容模式安装的模块");
                        if (AnsiConsole.Confirm("是否要覆盖该模块?"))
                        {
                            InstallModule(compatiableModulePath);
                        }
                        else
                        {
                            AnsiConsole.WriteLine("安装失败，请手动处理与其他模块的冲突");
                        }
                    }
                    else if (File.Exists(modulePath))
                    {
                        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(modulePath)));
                        if (UraCoreHelper.uraCoreHashs.Contains(hash) && AnsiConsole.Confirm("发现旧版本ura-core，是否更新？"))
                        {
                            InstallModule(modulePath);
                        }
                        else if (AnsiConsole.Confirm("发现其他模块，是否要以兼容模式安装？"))
                        {
                            InstallModule(compatiableModulePath);
                        }
                        else
                        {
                            AnsiConsole.WriteLine("安装失败，请手动处理与其他模块的冲突，或尝试兼容模式");
                        }
                    }
                    void InstallModule(string path)
                    {
                        using var fs = File.Create(path);
                        stream.CopyTo(fs);
                        fs.Flush();
                        fs.Close();
                        AnsiConsole.WriteLine($"安装到{path}成功,按任意键返回主菜单");
                    }
                }
                Console.ReadKey();
#pragma warning restore CA1416
            }
            AnsiConsole.Clear();

            return prompt;
        }
        static async Task ParseArguments(string[] args)
        {
            switch (args.Length)
            {
                case 0:
                    await ResourceUpdater.TryUpdateProgram();
                    return;
                case 1:
                    {
                        switch (args[0])
                        {
                            case "-v":
                            case "--version":
                                {
                                    Console.Write(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
                                    Environment.Exit(0);
                                    return;
                                }
                            case "--install-netfilter-driver":
                                {
                                    AnsiConsole.Status().Start("正在安装加速驱动，请勿关闭该窗口。", (ctx) =>
                                    {
                                        ctx.Spinner(Spinner.Known.BouncingBar);
                                        NetFilter.InstallDriver();
                                    });
                                    Environment.Exit(0);
                                    return;
                                }
                            case "--uninstall-netfilter-driver":
                                {
                                    AnsiConsole.Status().Start("正在卸载加速驱动，请勿关闭该窗口。", (ctx) =>
                                    {
                                        ctx.Spinner(Spinner.Known.BouncingBar);
                                        NetFilter.UninstallDriver();
                                    });
                                    Environment.Exit(0);
                                    return;
                                }
                            case "--cmder":
                                {
                                    runInCmder = true;
                                    return;
                                }
                        }
                        return;
                    }
                case 2:
                    {
                        switch (args[0])
                        {
                            case "--update":
                                {
                                    await ResourceUpdater.TryUpdateProgram(args[1]);
                                    return;
                                }
                            case "--update-data":
                                {
                                    System.IO.Compression.ZipFile.ExtractToDirectory(args[1], Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmamusumeResponseAnalyzer"));
                                    return;
                                }
                            case "--get-dmm-onetime-token":
                                {
                                    Console.Write(await DMM.Accounts[int.Parse(args[1])].GetExecuteArgsAsync());
                                    Environment.Exit(0);
                                    return;
                                }
                        }
                        return;
                    }
            }
        }
    }

    /// <summary>
    ///     Adapted from
    ///     http://stackoverflow.com/questions/13656846/how-to-programmatic-disable-c-sharp-console-applications-quick-edit-mode
    /// </summary>
    internal static class ConsoleHelper
    {
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_MOUSE_INPUT = 0x0010;

        // STD_INPUT_HANDLE (DWORD): -10 is the standard input device.
        private const int StdInputHandle = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        internal static bool DisableQuickEditMode()
        {
            var consoleHandle = GetStdHandle(StdInputHandle);

            // get current console mode
            if (!GetConsoleMode(consoleHandle, out var consoleMode))
                // ERROR: Unable to get console mode.
                return false;

            // Clear the quick edit bit in the mode flags
            consoleMode &= ~ENABLE_QUICK_EDIT_MODE;
            consoleMode &= ~ENABLE_MOUSE_INPUT;

            // set the new mode
            if (!SetConsoleMode(consoleHandle, consoleMode))
                // ERROR: Unable to set console mode
                return false;

            return true;
        }
    }
}