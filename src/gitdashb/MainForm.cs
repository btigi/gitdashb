/*
Surface the repoStatus stats (added, modified etc.)

Pull
*/

using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace gitdashb
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            MainForm_Resize(null, null);

            var config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();

            this.Left = Convert.ToInt32(config["left"]);
            this.Top = Convert.ToInt32(config["top"]);
            this.Width = Convert.ToInt32(config["width"]);
            this.Height = Convert.ToInt32(config["height"]);

            var repos = new ConfigurationBuilder()
                .AddJsonFile("repos.json")
                .Build();

            var repoInfos = repos.GetSection("Repos").Get<RepoInfo[]>();

            if (repoInfos == null)
            {
                MessageBox.Show("Repos could not be loaded from repos.json");
                Application.Exit();
                return;
            }

            foreach (var repoInfo in repoInfos)
            {
                var valid = Repository.IsValid(repoInfo.GitDirectory);
                if (valid)
                {
                    using var repository = new Repository(repoInfo.GitDirectory);
                    AddPanel(repoInfo, repository);
                }
                else
                {
                    AddPanel(repoInfo, null);
                }
            }
        }

        delegate void DisablePanelDelegate(int group, FlowLayoutPanel panel, Panel thisPanel, string outcome);
        DisablePanelDelegate disablePanelByGroup = (group, flowpanel, panel, outcome) =>
        {
            if (flowpanel.Controls.Count > 0)
            {
                foreach (var control in flowpanel.Controls)
                {
                    if (control is Control controlAsControl)
                    {
                        var tag = controlAsControl.Tag;
                        if (tag != null && (int)tag == group)
                        {
                            (control as Panel).Enabled = false;
                        }
                    }
                }

                var o = panel.Controls.Find("outcome", true)[0];
                o.Visible = false;

                var p = panel.Controls.Find("progress", true)[0];
                p.Visible = true;
            }
        };

        DisablePanelDelegate enablePanelByGroup = (group, flowpanel, panel, outcome) =>
        {
            if (flowpanel.Controls.Count > 0)
            {
                foreach (var control in flowpanel.Controls)
                {
                    if (control is Control controlAsControl)
                    {
                        var tag = controlAsControl.Tag;
                        if (tag != null && (int)tag == group)
                        {
                            (control as Panel).Enabled = true;
                        }
                    }
                }

                var o = panel.Controls.Find("outcome", true)[0];
                var p = panel.Controls.Find("progress", true)[0];
                p.Visible = false;
                o.Text = outcome;
                o.Visible = true;
            }
        };

        public void AddPanel(RepoInfo repoInfo, Repository repository)
        {
            var repoStatus = repository?.RetrieveStatus();
            var status = (repoStatus?.IsDirty ?? false) ? "*" : "-";

            //var untrackedCount = repoStatus.Added.Count();
            //var modifiedCount = repoStatus.Modified.Count();
            //var stagedCount = repoStatus.Staged.Count();
            //var ignoredCount = repoStatus.Ignored.Count();
            //repository?.Head.TrackedBranch.TrackingDetails.AheadBy
            //repository?.Head.TrackedBranch.TrackingDetails.BehindBy

            var panel = new Panel();
            panel.Height = 200;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Tag = repoInfo.Group;

            var lblName = new Label();
            lblName.Text = repoInfo.Name;

            var lblBranch = new Label();
            lblBranch.Text = $"{repository?.Head?.FriendlyName} {status}";
            lblBranch.Top = lblName.Bottom + 10;

            var btnNuke = new Button();
            btnNuke.Text = "Nuke";
            btnNuke.Click += (s, e) => { _ = DiscardFileChanges(repoInfo, panel, repoStatus?.Added.Select(s => s.FilePath), disablePanelByGroup, enablePanelByGroup); };
            btnNuke.Top = lblBranch.Bottom + 10;

            var btnClean = new Button();
            btnClean.Text = "Clean";
            btnClean.Click += (s, e) => { _ = DotNetClean(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnClean.Top = btnNuke.Bottom + 10;

            var btnRestore = new Button();
            btnRestore.Text = "Restore";
            btnRestore.Click += (s, e) => { _ = DotNetRestore(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnRestore.Top = btnClean.Bottom + 10;

            var btnBuild = new Button();
            btnBuild.Text = "Build";
            btnBuild.Click += (s, e) => { _ = DotNetBuild(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnBuild.Top = btnRestore.Bottom + 10;

            var btnFetch = new Button();
            btnFetch.Text = "Fetch";
            btnFetch.Click += (s, e) => { _ = Fetch(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnFetch.Top = btnBuild.Bottom + 10;

            var btnCmdPrompt = new Button();
            btnCmdPrompt.Text = "Cmd";
            btnCmdPrompt.Click += (s, e) => { _ = CmdPrompt(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnCmdPrompt.Top = btnFetch.Bottom + 10;

            var btnClone = new Button();
            btnClone.Text = "Clone";
            btnClone.Click += (s, e) => { _ = Clone(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnClone.Top = btnFetch.Bottom + 10;

            var btnRun = new Button();
            btnRun.Text = "Run";
            btnRun.Click += (s, e) => { _ = DotNetRun(repoInfo, panel, disablePanelByGroup, enablePanelByGroup); };
            btnRun.Top = btnClone.Bottom + 10;

            var progressBar = new ProgressBar();
            progressBar.Value = 0;
            progressBar.Top = btnClone.Bottom;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = false;
            progressBar.Name = "progress";
            progressBar.MarqueeAnimationSpeed = 1;

            var lblOutcome = new Label();
            lblOutcome.Text = $"Outcome: ";
            lblOutcome.Top = btnBuild.Bottom;
            lblOutcome.Visible = false;
            lblOutcome.Name = "outcome";


            var flow = new FlowLayoutPanel();
            flow.Width = panel.Width;
            flow.Height = panel.Height;

            flow.Controls.Add(lblName);
            flow.Controls.Add(lblBranch);
            flow.Controls.Add(btnNuke);
            flow.Controls.Add(btnClean);
            flow.Controls.Add(btnRestore);
            flow.Controls.Add(btnBuild);
            flow.Controls.Add(btnFetch);
            flow.Controls.Add(btnCmdPrompt);
            flow.Controls.Add(btnClone);
            flow.Controls.Add(btnRun);
            flow.Controls.Add(progressBar);
            flow.Controls.Add(lblOutcome);
            flow.BackColor = Color.White;
            panel.Controls.Add(flow);

            flow.Padding = new Padding(10);
            flow.SetFlowBreak(lblBranch, true);

            flowLayoutPanel.Controls.Add(panel);
        }

        // Delete Added but comitted
        async Task DiscardFileChanges(RepoInfo repoInfo, Panel panel, IEnumerable<string> filePaths, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            using (var repository = new Repository(repoInfo.GitDirectory))
            {
                var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
                repository.CheckoutPaths(repository.Head.FriendlyName, filePaths, options);
            }

            var resultText = $"[{DateTime.Now:HH:mm:ss}] success";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task Fetch(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            using (var repository = new Repository(repoInfo.GitDirectory))
            {
                Commands.Fetch(repository, repoInfo.Remote, Enumerable.Empty<string>(), new FetchOptions()
                {
                    CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials()
                    {
                        Username = repoInfo.SourceUsername,
                        Password = repoInfo.SourcePassword
                    }
                }, $"Fetch at {DateTime.Now}");
            }

            var resultText = $"[{DateTime.Now:HH:mm:ss}] success";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task Clone(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                Repository.Clone(repoInfo.SourceUrl, repoInfo.GitDirectory, new CloneOptions()
                {
                    FetchOptions =
                    {
                        CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials()
                        {
                            Username = repoInfo.SourceUsername,
                            Password = repoInfo.SourcePassword
                        }
                    }
                });
            }
            catch
            {
                exitCode = -1;
            }

            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task DotNetClean(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "dotnet";
                startInfo.Arguments = "clean";
                startInfo.WorkingDirectory = repoInfo.SlnDirectory;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Process proc = Process.Start(startInfo);
                await Task.Run(() => proc.WaitForExit());
                exitCode = proc.ExitCode;
            }
            catch
            {
                exitCode = -1;
            }
            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task DotNetRestore(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "dotnet";
                startInfo.Arguments = "restore";
                startInfo.WorkingDirectory = repoInfo.SlnDirectory;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Process proc = Process.Start(startInfo);
                await Task.Run(() => proc.WaitForExit());
                exitCode = proc.ExitCode;
            }
            catch
            {
                exitCode = -1;
            }
            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task DotNetBuild(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "dotnet";
                startInfo.Arguments = "build";
                startInfo.WorkingDirectory = repoInfo.SlnDirectory;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                Process proc = Process.Start(startInfo);
                await Task.Run(() => proc.WaitForExit());
                exitCode = proc.ExitCode;
            }
            catch
            {
                exitCode = -1;
            }

            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task DotNetRun(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "dotnet";
                startInfo.Arguments = "run";
                startInfo.WorkingDirectory = repoInfo.RunDirectory;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;

                Process.Start(startInfo);
            }
            catch
            {
                exitCode = -1;
            }
            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        async Task CmdPrompt(RepoInfo repoInfo, Panel panel, DisablePanelDelegate disableAction, DisablePanelDelegate enableAction)
        {
            disableAction((int)panel.Tag, flowLayoutPanel, panel, "");

            var exitCode = 0;
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd";
                startInfo.WorkingDirectory = repoInfo.SlnDirectory;

                Process.Start(startInfo);
            }
            catch
            {
                exitCode = -1;
            }

            var resultText = exitCode == 0 ? "success" : $"failure ({exitCode})";
            resultText = $"[{DateTime.Now:HH:mm:ss}] {resultText}";
            enableAction((int)panel.Tag, flowLayoutPanel, panel, resultText);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            flowLayoutPanel.Width = this.Width;
            flowLayoutPanel.Height = this.Height;
        }
    }

    public class RepoInfo
    {
        public string Name { get; set; } = "";
        public string GitDirectory { get; set; } = "";
        public string SlnDirectory { get; set; } = "";
        public string RunDirectory { get; set; } = "";
        public string GoldenBranch { get; set; } = "";
        public string Remote { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public int Group { get; set; } = 0;
        public string SourceUsernameEnvVar { get; set; } = "";
        public string SourcePasswordEnvVar { get; set; } = "";
        public string SourceUsername { get { return Environment.GetEnvironmentVariable(SourceUsernameEnvVar, EnvironmentVariableTarget.Machine) ?? String.Empty; } }
        public string SourcePassword { get { return Environment.GetEnvironmentVariable(SourcePasswordEnvVar, EnvironmentVariableTarget.Machine) ?? String.Empty; } }
    }
}