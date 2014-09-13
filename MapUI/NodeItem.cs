using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Collections.ObjectModel;
using System.Collections;
using System.Collections.Specialized;
using System.Windows.Media;
using System.Windows.Input;
using Utils;
using System.Diagnostics;

namespace MapUI
{
    /// <summary>
    /// This is a UI element that represents a Map/flow-chart node.
    /// </summary>
    public class NodeItem : ListBoxItem
    {
        #region Dependency Property/Event Definitions

        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register("X", typeof(double), typeof(NodeItem),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register("Y", typeof(double), typeof(NodeItem),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ZIndexProperty =
            DependencyProperty.Register("ZIndex", typeof(int), typeof(NodeItem),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        internal static readonly DependencyProperty ParentMapViewProperty =
            DependencyProperty.Register("ParentMapView", typeof(MapView), typeof(NodeItem), 
                new FrameworkPropertyMetadata(ParentMapView_PropertyChanged));

        internal static readonly RoutedEvent NodeDragStartedEvent =
            EventManager.RegisterRoutedEvent("NodeDragStarted", RoutingStrategy.Bubble, typeof(NodeDragStartedEventHandler), typeof(NodeItem));

        internal static readonly RoutedEvent NodeDraggingEvent =
            EventManager.RegisterRoutedEvent("NodeDragging", RoutingStrategy.Bubble, typeof(NodeDraggingEventHandler), typeof(NodeItem));

        internal static readonly RoutedEvent NodeDragCompletedEvent =
            EventManager.RegisterRoutedEvent("NodeDragCompleted", RoutingStrategy.Bubble, typeof(NodeDragCompletedEventHandler), typeof(NodeItem));

        #endregion Dependency Property/Event Definitions

        public NodeItem()
        {
            //
            // By default, we don't want this UI element to be focusable.
            //
            Focusable = false;
        }

        /// <summary>
        /// The X coordinate of the node.
        /// </summary>
        public double X
        {
            get
            {
                return (double)GetValue(XProperty);
            }
            set
            {
                SetValue(XProperty, value);
            }
        }

        /// <summary>
        /// The Y coordinate of the node.
        /// </summary>
        public double Y
        {
            get
            {
                return (double)GetValue(YProperty);
            }
            set
            {
                SetValue(YProperty, value);
            }
        }

        /// <summary>
        /// The Z index of the node.
        /// </summary>
        public int ZIndex
        {
            get
            {
                return (int)GetValue(ZIndexProperty);
            }
            set
            {
                SetValue(ZIndexProperty, value);
            }
       }            

        #region Private Data Members\Properties

        /// <summary>
        /// Reference to the data-bound parent MapView.
        /// </summary>
        internal MapView ParentMapView
        {
            get
            {
                return (MapView)GetValue(ParentMapViewProperty);
            }
            set
            {
                SetValue(ParentMapViewProperty, value);
            }
        }

        /// <summary>
        /// The point the mouse was last at when dragging.
        /// </summary>
        private Point lastMousePoint;

        /// <summary>
        /// Set to 'true' when left mouse button is held down.
        /// </summary>
        private bool isLeftMouseDown = false;

        /// <summary>
        /// Set to 'true' when left mouse button and the control key are held down.
        /// </summary>
        private bool isLeftMouseAndControlDown = false;

        /// <summary>
        /// Set to 'true' when dragging has started.
        /// </summary>
        private bool isDragging = false;

        /// <summary>
        /// The threshold distance the mouse-cursor must move before dragging begins.
        /// </summary>
        private static readonly double DragThreshold = 5;

        #endregion Private Data Members\Properties

        #region Private Methods

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeItem), new FrameworkPropertyMetadata(typeof(NodeItem)));
        }

        /// <summary>
        /// Bring the node to the front of other elements.
        /// </summary>
        internal void BringToFront()
        {
            if (this.ParentMapView == null)
            {
                return;
            }

            int maxZ = this.ParentMapView.FindMaxZIndex();
            this.ZIndex = maxZ + 1;
        }

        /// <summary>
        /// Called when a mouse button is held down.
        /// </summary>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            BringToFront();

            if (this.ParentMapView != null)
            {
                this.ParentMapView.Focus();
            }

            if (e.ChangedButton == MouseButton.Left && this.ParentMapView != null)
            {
                lastMousePoint = e.GetPosition(this.ParentMapView);
                isLeftMouseDown = true;

                LeftMouseDownSelectionLogic();

                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right && this.ParentMapView != null)
            {
                RightMouseDownSelectionLogic();
           }
        }

        /// <summary>
        /// This method contains selection logic that is invoked when the left mouse button is pressed down.
        /// The reason this exists in its own method rather than being included in OnMouseDown is 
        /// so that ConnectorItem can reuse this logic from its OnMouseDown.
        /// </summary>
        internal void LeftMouseDownSelectionLogic()
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                //
                // Control key was held down.
                // This means that the rectangle is being added to or removed from the existing selection.
                // Don't do anything yet, we will act on this later in the MouseUp event handler.
                //
                isLeftMouseAndControlDown = true;
            }
            else
            {
                //
                // Control key is not held down.
                //
                isLeftMouseAndControlDown = false;

                if (this.ParentMapView.SelectedNodes.Count == 0)
                {
                    //
                    // Nothing already selected, select the item.
                    //
                    this.IsSelected = true;
                }
                else if (this.ParentMapView.SelectedNodes.Contains(this) ||
                         this.ParentMapView.SelectedNodes.Contains(this.DataContext))
                {
                    // 
                    // Item is already selected, do nothing.
                    // We will act on this in the MouseUp if there was no drag operation.
                    //
                }
                else
                {
                    //
                    // Item is not selected.
                    // Deselect all, and select the item.
                    //
                    this.ParentMapView.SelectedNodes.Clear();
                    this.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// This method contains selection logic that is invoked when the right mouse button is pressed down.
        /// The reason this exists in its own method rather than being included in OnMouseDown is 
        /// so that ConnectorItem can reuse this logic from its OnMouseDown.
        /// </summary>
        internal void RightMouseDownSelectionLogic()
        {
            if (this.ParentMapView.SelectedNodes.Count == 0)
            {
                //
                // Nothing already selected, select the item.
                //
                this.IsSelected = true;
            }
            else if (this.ParentMapView.SelectedNodes.Contains(this) ||
                     this.ParentMapView.SelectedNodes.Contains(this.DataContext))
            {
                // 
                // Item is already selected, do nothing.
                //
            }
            else
            {
                //
                // Item is not selected.
                // Deselect all, and select the item.
                //
                this.ParentMapView.SelectedNodes.Clear();
                this.IsSelected = true;
            }
        }

        /// <summary>
        /// Called when the mouse cursor is moved.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDragging)
            {
                //
                // Raise the event to notify that dragging is in progress.
                //

                Point curMousePoint = e.GetPosition(this.ParentMapView);

                object item = this;
                if (DataContext != null)
                {
                    item = DataContext;
                }

                Vector offset = curMousePoint - lastMousePoint;
                if (offset.X != 0.0 ||
                    offset.Y != 0.0)
                {
                    lastMousePoint = curMousePoint;

                    RaiseEvent(new NodeDraggingEventArgs(NodeDraggingEvent, this, new object[] { item }, offset.X, offset.Y));
                }
            }
            else if (isLeftMouseDown && this.ParentMapView.EnableNodeDragging)
            {
                //
                // The user is left-dragging the node,
                // but don't initiate the drag operation until 
                // the mouse cursor has moved more than the threshold distance.
                //
                Point curMousePoint = e.GetPosition(this.ParentMapView);
                var dragDelta = curMousePoint - lastMousePoint;
                double dragDistance = Math.Abs(dragDelta.Length);
                if (dragDistance > DragThreshold)
                {
                    //
                    // When the mouse has been dragged more than the threshold value commence dragging the node.
                    //

                    //
                    // Raise an event to notify that that dragging has commenced.
                    //
                    NodeDragStartedEventArgs eventArgs = new NodeDragStartedEventArgs(NodeDragStartedEvent, this, new NodeItem[] { this });
                    RaiseEvent(eventArgs);

                    if (eventArgs.Cancel)
                    {
                        //
                        // Handler of the event disallowed dragging of the node.
                        //
                        isLeftMouseDown = false;
                        isLeftMouseAndControlDown = false;
						return;
                    }

                    isDragging = true;
                    this.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Called when a mouse button is released.
        /// </summary>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (isLeftMouseDown)
            {
                if (isDragging)
                {
                    //
                    // Raise an event to notify that node dragging has finished.
                    //

                    RaiseEvent(new NodeDragCompletedEventArgs(NodeDragCompletedEvent, this, new NodeItem[] { this }));

					this.ReleaseMouseCapture();

                    isDragging = false;
                }
                else
                {
                    //
                    // Execute mouse up selection logic only if there was no drag operation.
                    //

                    LeftMouseUpSelectionLogic();
                }

                isLeftMouseDown = false;
                isLeftMouseAndControlDown = false;

                e.Handled = true;
            }
        }

        /// <summary>
        /// This method contains selection logic that is invoked when the left mouse button is released.
        /// The reason this exists in its own method rather than being included in OnMouseUp is 
        /// so that ConnectorItem can reuse this logic from its OnMouseUp.
        /// </summary>
        internal void LeftMouseUpSelectionLogic()
        {
            if (isLeftMouseAndControlDown)
            {
                //
                // Control key was held down.
                // Toggle the selection.
                //
                this.IsSelected = !this.IsSelected;
            }
            else
            {
                //
                // Control key was not held down.
                //
                if (this.ParentMapView.SelectedNodes.Count == 1 &&
                    (this.ParentMapView.SelectedNode == this ||
                     this.ParentMapView.SelectedNode == this.DataContext))
                {
                    //
                    // The item that was clicked is already the only selected item.
                    // Don't need to do anything.
                    //
                }
                else
                {
                    //
                    // Clear the selection and select the clicked item as the only selected item.
                    //
                    this.ParentMapView.SelectedNodes.Clear();
                    this.IsSelected = true;
                }
            }

            isLeftMouseAndControlDown = false;
        }

        /// <summary>
        /// Event raised when the ParentMapView property has changed.
        /// </summary>
        private static void ParentMapView_PropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            //
            // Bring new nodes to the front of the z-order.
            //
            var nodeItem = (NodeItem) o;
            nodeItem.BringToFront();
        }

        #endregion Private Methods
    }
}
