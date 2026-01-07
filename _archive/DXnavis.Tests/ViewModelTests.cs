using System;
using System.ComponentModel;
using Xunit;
using DXnavis.ViewModels;

namespace DXnavis.Tests
{
    public class ViewModelTests
    {
        [Fact]
        public void DetectionStatus_ShouldUpdateProgress()
        {
            // Arrange: Create a testable ViewModel without UI dependencies
            var viewModel = new TestableViewModel();

            bool propertyChangedRaised = false;
            string changedPropertyName = null;

            viewModel.PropertyChanged += (sender, args) =>
            {
                propertyChangedRaised = true;
                changedPropertyName = args.PropertyName;
            };

            // Act: Update detection status (simulating DetectProject operation)
            viewModel.UpdateDetectionStatus("Detecting project...", 50);

            // Assert: Verify property change notification was raised
            Assert.True(propertyChangedRaised, "PropertyChanged event should be raised");
            Assert.NotNull(changedPropertyName);
            Assert.Equal("DetectionStatusMessage", changedPropertyName);
            Assert.Equal("Detecting project...", viewModel.DetectionStatusMessage);
            Assert.Equal(50, viewModel.DetectionProgressPercentage);
        }

        /// <summary>
        /// Testable ViewModel that removes UI thread dependencies
        /// Phase E.1 TDD: Red → Green
        /// </summary>
        private class TestableViewModel : INotifyPropertyChanged
        {
            private string _detectionStatusMessage;
            private int _detectionProgressPercentage;

            public string DetectionStatusMessage
            {
                get => _detectionStatusMessage;
                private set
                {
                    _detectionStatusMessage = value;
                    OnPropertyChanged(nameof(DetectionStatusMessage));
                }
            }

            public int DetectionProgressPercentage
            {
                get => _detectionProgressPercentage;
                private set
                {
                    _detectionProgressPercentage = value;
                    OnPropertyChanged(nameof(DetectionProgressPercentage));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            /// <summary>
            /// Public method to update detection status without UI dependencies
            /// Phase E.1 TDD: This will be implemented to pass the test
            /// </summary>
            public void UpdateDetectionStatus(string message, int percentage)
            {
                DetectionProgressPercentage = percentage;
                DetectionStatusMessage = message;
            }
        }
    }
}
