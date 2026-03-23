using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using CCPad.Terminal;
using Windows.Foundation;
using Windows.UI;

namespace CCPad.Controls
{
    public sealed class GridSplitter : Panel
    {
        private const double SplitterThickness = 4;
        private const double MinPaneSize = 80;

        private static readonly SolidColorBrush NormalBrush =
            new(Color.FromArgb(255, 42, 42, 42));
        private static readonly SolidColorBrush HoverBrush =
            new(Color.FromArgb(255, 85, 85, 85));

        private readonly SplitOrientation _orientation;
        private readonly SplitContainerNode _node;
        private Grid? _parentGrid;
        private Point _dragStart;
        private double _startRatio;
        private bool _dragging;

        public GridSplitter(SplitOrientation orientation, SplitContainerNode node)
        {
            _orientation = orientation;
            _node = node;

            Background = NormalBrush;

            if (_orientation == SplitOrientation.Vertical)
            {
                Width = SplitterThickness;
                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                Height = SplitterThickness;
                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Stretch;
            }

            PointerEntered += OnPointerEntered;
            PointerExited += OnPointerExited;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Background = HoverBrush;
            ProtectedCursor = _orientation == SplitOrientation.Vertical
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast)
                : InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging)
            {
                Background = NormalBrush;
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _parentGrid = Parent as Grid;
            if (_parentGrid == null) return;

            _dragging = true;
            _dragStart = e.GetCurrentPoint(_parentGrid).Position;
            _startRatio = _node.SplitRatio;
            CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_dragging || _parentGrid == null) return;

            var pos = e.GetCurrentPoint(_parentGrid).Position;
            double totalSize;
            double delta;

            if (_orientation == SplitOrientation.Vertical)
            {
                totalSize = _parentGrid.ActualWidth - SplitterThickness;
                delta = pos.X - _dragStart.X;
            }
            else
            {
                totalSize = _parentGrid.ActualHeight - SplitterThickness;
                delta = pos.Y - _dragStart.Y;
            }

            if (totalSize <= 0) return;

            double newRatio = _startRatio + delta / totalSize;

            // Enforce minimum pane sizes
            double minRatio = MinPaneSize / totalSize;
            double maxRatio = 1.0 - minRatio;
            newRatio = System.Math.Clamp(newRatio, minRatio, maxRatio);

            _node.SplitRatio = newRatio;
            ApplyRatio(newRatio);
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            StopDrag(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            StopDrag(null);
        }

        private void StopDrag(Pointer? pointer)
        {
            if (!_dragging) return;
            _dragging = false;
            if (pointer != null) ReleasePointerCapture(pointer);
            Background = NormalBrush;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        private void ApplyRatio(double ratio)
        {
            if (_parentGrid == null) return;

            if (_orientation == SplitOrientation.Vertical)
            {
                if (_parentGrid.ColumnDefinitions.Count >= 3)
                {
                    _parentGrid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
                    _parentGrid.ColumnDefinitions[2].Width = new GridLength(1.0 - ratio, GridUnitType.Star);
                }
            }
            else
            {
                if (_parentGrid.RowDefinitions.Count >= 3)
                {
                    _parentGrid.RowDefinitions[0].Height = new GridLength(ratio, GridUnitType.Star);
                    _parentGrid.RowDefinitions[2].Height = new GridLength(1.0 - ratio, GridUnitType.Star);
                }
            }
        }
    }
}
