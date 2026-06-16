using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace TimerAlarmPlugin
{
    public class Main : IPlugin
    {
        private PluginInitContext? _context;
        private readonly List<Display> activeWindows = new();

        private System.Timers.Timer? visibilityTimer;
        private bool lastVisible;

        public void Init(PluginInitContext context)
        {
            _context = context;
            StartVisibilityWatcher();
        }

        public List<Result> Query(Query query)
        {
            string input = (query.Search ?? "").Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Delete by id",
                        SubTitle = "Usage: del [id]",
                        IcoPath = "icon.png",
                        Action = _ =>
                        {
                            _context?.API.ChangeQuery($"{query.ActionKeyword} del ", true);
                            return false;
                        }
                    }
                };
            }

            var delete = TryCreateDeleteResult(input);
            if (delete != null)
                return delete;

            bool isAlarm = query.ActionKeyword == "alarm";

            var parsed = ParseNameAndTime(input);
            int time = ParseTime(parsed.Time, !isAlarm);

            var results = new List<Result>();

            string subtitle = isAlarm ? "Start alarm" : "Start timer";
            string title = FormatTime(time);

            if (!string.IsNullOrWhiteSpace(parsed.Name))
                subtitle += $" ({parsed.Name})";

            if (IdExists(parsed.Name))
            {
                results.Add(new Result
                {
                    Title = $"\"{parsed.Name}\" already exists",
                    SubTitle = "Choose another name or delete the existing timer.",
                    IcoPath = "icon.png",
                    Action = _ => false
                });
            }
            else
            {
                results.Add(new Result
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = "icon.png",
                    Action = _ => CreateWindow(time, !isAlarm, parsed.Name)
                });
            }

            if (TryParseExistingId(input, out string existingId))
            {
                results.Add(new Result
                {
                    Title = $"Delete {existingId}",
                    SubTitle = $"Remove {(isAlarm ? "alarm" : "timer")} {existingId}",
                    IcoPath = "icon.png",
                    Action = _ => RemoveById(existingId)
                });
            }

            return results;
        }

        private (string? Name, string Time) ParseNameAndTime(string input)
        {
            input = input.Trim();

            if (string.IsNullOrWhiteSpace(input))
                return (null, "");

            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                if (parts[0].Any(char.IsDigit))
                    return (null, parts[0]);

                return (parts[0], "");
            }

            if (parts[0].Any(char.IsDigit))
                return (null, input);

            return (parts[0], parts[1]);
        }

        private List<Result>? TryCreateDeleteResult(string input)
        {
            if (!input.StartsWith("del", StringComparison.OrdinalIgnoreCase))
                return null;

            string rest = input[3..].Trim();

            string id = rest.Trim();

            if (string.IsNullOrWhiteSpace(id))
            {
                return new()
                {
                    new Result
                    {
                        Title = "Delete: invalid id",
                        SubTitle = "Usage: del [id]",
                        IcoPath = "icon.png",
                        Action = _ => false
                    }
                };
            }

            return new()
            {
                new Result
                {
                    Title = $"Delete {id}",
                    SubTitle = $"Remove timer/alarm {id}",
                    IcoPath = "icon.png",
                    Action = _ => RemoveById(id)
                }
            };
        }

        private bool TryParseExistingId(string input, out string id)
        {
            id = input.Trim();

            string searchId = id;

            return activeWindows.Any(w =>
                string.Equals(w.Id, searchId, StringComparison.OrdinalIgnoreCase));
        }

        private int ParseTime(string input, bool timerType)
        {
            input = input.Replace(" ", ":");
            input = Regex.Replace(input, @"[^0-9:]", "");

            var nums = input
                .Split(':')
                .Take(3)
                .Select(x =>
                {
                    int.TryParse(x, out int n);
                    return n;
                })
                .ToList();

            int h, m, s;

            if (timerType)
            {
                s = nums.Count > 0 ? nums[^1] : 0;
                m = nums.Count > 1 ? nums[^2] : 0;
                h = nums.Count > 2 ? nums[^3] : 0;
            }
            else
            {
                h = nums.Count > 0 ? nums[0] : 0;
                m = nums.Count > 1 ? nums[1] : 0;
                s = nums.Count > 2 ? nums[2] : 0;
            }

            int seconds = h * 3600 + m * 60 + s;

            return Math.Min(seconds, 7 * 24 * 60 * 60);
        }

        private string FormatTime(int seconds)
        {
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;

            return $"{h:D2}:{m:D2}:{s:D2}";
        }

        private bool CreateWindow(int time, bool timerType, string? name)
        {
            Display display = new();

            display.TypeText.Text = timerType ? "Timer" : "Alarm";
            display.TimeText.Text = FormatTime(time);

            string id = string.IsNullOrWhiteSpace(name)
                ? GetSmallestAvailableId()
                : name;

            display.SetId(id);

            activeWindows.Add(display);

            display.SetPosition(activeWindows.Count - 1);

            display.Show();
            display.Visibility = Visibility.Hidden;

            int initialSeconds;

            if (timerType)
            {
                initialSeconds = time;
            }
            else
            {
                int now = (int)DateTime.Now.TimeOfDay.TotalSeconds;
                int day = 24 * 3600;

                initialSeconds = ((time - now) % day + day) % day;

                if (initialSeconds == 0)
                    initialSeconds = day;
            }

            display.Start(initialSeconds, !timerType, d =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => d.Close());
            });

            display.Closed += (_, _) =>
            {
                activeWindows.Remove(display);
                RepositionAllWindows();
            };

            return true;
        }

        private string GetSmallestAvailableId()
        {
            var used = new HashSet<string>(activeWindows.Select(w => w.Id));

            string id = "1";
            while (used.Contains(id))
                id = (int.Parse(id) + 1).ToString();

            return id;
        }

        private void RepositionAllWindows()
        {
            for (int i = 0; i < activeWindows.Count; i++)
            {
                activeWindows[i].SetPosition(i);
            }
        }

        private void StartVisibilityWatcher()
        {
            visibilityTimer = new System.Timers.Timer(100);

            visibilityTimer.Elapsed += (_, _) =>
            {
                bool visible = _context?.API.IsMainWindowVisible() ?? false;

                if (visible == lastVisible)
                    return;

                lastVisible = visible;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (visible)
                        ShowAllWindows();
                    else
                        HideAllWindows();
                });
            };

            visibilityTimer.Start();
        }

        private void ShowAllWindows()
        {
            RepositionAllWindows();

            foreach (var window in activeWindows)
            {
                if (!window.IsVisible)
                    window.ShowOverlay();
            }
        }

        private void HideAllWindows()
        {
            foreach (var window in activeWindows)
            {
                if (window.IsVisible)
                    window.Visibility = Visibility.Hidden;
            }
        }

        private bool RemoveById(string id)
        {
            var display = activeWindows.FirstOrDefault(w =>
                string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));

            if (display == null)
                return false;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    display.Close();
                }
                catch
                {
                }
            });

            return true;
        }

        private bool IdExists(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return activeWindows.Any(w =>
                string.Equals(w.Id, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}