using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SystemOptimizer.Models
{
    public abstract class TweakBase : ObservableObject, ITweak
    {
        public string Id { get; }
        public TweakCategory Category { get; }
        public string Title { get; }
        public string Description { get; }

        private TweakStatus _status;
        public TweakStatus Status
        {
            get => _status;
            protected set
            {
                SetProperty(ref _status, value);
                OnPropertyChanged(nameof(IsOptimized));
            }
        }

        public bool IsOptimized => Status == TweakStatus.Optimized;

        protected TweakBase(string id, TweakCategory category, string title, string description)
        {
            Id = id;
            Category = category;
            Title = title;
            Description = description;
            Status = TweakStatus.Unknown;
        }

        public abstract (bool Success, string Message) Apply();
        public abstract (bool Success, string Message) Revert();
        public abstract void CheckStatus();
    }
}
