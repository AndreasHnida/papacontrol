﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Timers;
using Avalonia.Threading;
using ReactiveUI;
using System.Text;
using System.Reactive;

namespace PapaControlApp.ViewModels
{

    public partial class MainWindowViewModel : ReactiveObject
    {


        private const string SettingsFilePath = "settings.dat";
        private readonly byte[] _encryptionKey = Encoding.UTF8.GetBytes("qwertzuiopasdfgh");

        private Stopwatch _stopwatch;
        private Timer _timer;
        private long _totalElapsedSeconds;
        private string _elapsedTime;
        private string _remainingTime;
        private string _allowedTime = "1h";

        private bool _isInputEnabled = false;

        public void ToggleInput()
        {
            IsInputEnabled = !IsInputEnabled;
        }
        public MainWindowViewModel()
        {
            LoadSettings();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();

            _timer = new Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();
        }

        public string ElapsedTime
        {
            get => _elapsedTime;
            set => this.RaiseAndSetIfChanged(ref _elapsedTime, value);
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set => this.RaiseAndSetIfChanged(ref _remainingTime, value);
        }

        public string AllowedTime
        {
            get => _allowedTime;
            set
            {
                this.RaiseAndSetIfChanged(ref _allowedTime, value);
                SaveSettings();
            }
        }
        private string _toggleButtonText = "Unlock Input";
        public string ToggleButtonText
        {
            get => _toggleButtonText;
            set => this.RaiseAndSetIfChanged(ref _toggleButtonText, value);
        }
        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _isInputEnabled, value);
                ToggleButtonText = value ? "Lock Input" : "Unlock Input";
            }
        }
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _totalElapsedSeconds += (long)_stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            if (TryParseHumanReadableTime(_allowedTime, out TimeSpan allowedTimeSpan))
            {
                TimeSpan elapsedTimeSpan = TimeSpan.FromSeconds(_totalElapsedSeconds);
                TimeSpan remainingTimeSpan = allowedTimeSpan - elapsedTimeSpan;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ElapsedTime = FormatTimeSpan(elapsedTimeSpan);
                    RemainingTime = FormatTimeSpan(remainingTimeSpan);
                    SaveSettings();
                });

                if (remainingTimeSpan <= TimeSpan.Zero)
                {
                    Debug.WriteLine("Time's up!");
                }
            }
            else
            {
                Debug.WriteLine("Invalid _allowedTime format.");
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        private bool TryParseHumanReadableTime(string input, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;
            var regex = new Regex(@"((?<h>\d+)h)?\s*((?<m>\d+)m)?\s*((?<s>\d+)s)?");
            var match = regex.Match(input);
            if (!match.Success) return false;

            int hours = 0, minutes = 0, seconds = 0;
            if (match.Groups["h"].Success) hours = int.Parse(match.Groups["h"].Value);
            if (match.Groups["m"].Success) minutes = int.Parse(match.Groups["m"].Value);
            if (match.Groups["s"].Success) seconds = int.Parse(match.Groups["s"].Value);

            timeSpan = new TimeSpan(hours, minutes, seconds);
            return true;
        }

        private void SaveSettings()
        {
            try
            {
                var data = $"{_allowedTime}|{_totalElapsedSeconds}";
                var encryptedData = EncryptString(data, _encryptionKey);
                File.WriteAllBytes(SettingsFilePath, encryptedData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var encryptedData = File.ReadAllBytes(SettingsFilePath);
                    var data = DecryptString(encryptedData, _encryptionKey);
                    var parts = data.Split('|');
                    if (parts.Length >= 2)
                    {
                        _allowedTime = parts[0];
                        _totalElapsedSeconds = long.Parse(parts[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }
        private byte[] EncryptString(string plainText, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                var iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
                using (var ms = new MemoryStream())
                {
                    ms.Write(iv, 0, iv.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return ms.ToArray();
                }
            }
        }


        private string DecryptString(byte[] cipherText, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                var iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherText, iv, iv.Length);

                using (var decryptor = aes.CreateDecryptor(aes.Key, iv))
                using (var ms = new MemoryStream(cipherText, iv.Length, cipherText.Length - iv.Length))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
