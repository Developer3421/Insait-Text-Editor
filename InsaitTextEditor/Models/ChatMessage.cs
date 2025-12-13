using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private int _id;
        private string _sender = string.Empty; // "User" or "Insait Assistant"
        private string _content = string.Empty;
        private DateTime _timestamp = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string Sender
        {
            get => _sender;
            set => SetField(ref _sender, value);
        }

        public string Content
        {
            get => _content;
            set => SetField(ref _content, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetField(ref _timestamp, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public string LocalizedSender
        {
            get
            {
                if (Sender == "User")
                    return LocalizationService.GetString("Key.ChatUser", "User");
                else
                    return LocalizationService.GetString("Key.ChatAssistant", "Assistant");
            }
        }

        public void RefreshLocalized()
        {
            OnPropertyChanged("LocalizedSender");
        }
    }
}