using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScumMiniMap {
    internal sealed class HoldAdvance {
        bool down,used;
        internal bool Press() { if(down)return false;down=true;used=false;return true; }
        internal bool Tick(long elapsed) { if(!down||used||elapsed<1000)return false;used=true;return true; }
        internal void Release() { down=false;used=false; }
    }
    internal static class HoldToProceed {
        internal static Button Button(Window window,string label,Action advance,bool primary=false) {
            var button=WpfFieldTheme.Button(label,()=>{},primary);button.ToolTip=Localization.Get("WizardHoldNext");
            var clock=new System.Diagnostics.Stopwatch();var gesture=new HoldAdvance();bool mouse=false,finished=false;
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(25) };
            Action cancel=()=> { timer.Stop();clock.Stop();gesture.Release();button.Content=label;if(button.IsMouseCaptured)button.ReleaseMouseCapture(); };
            Action<bool> begin=isMouse=> { if(finished||!button.IsEnabled||!gesture.Press())return;mouse=isMouse;clock.Restart();timer.Start(); };
            timer.Tick+=(s,e)=> {
                if(mouse&&(!button.IsMouseOver||Mouse.LeftButton!=MouseButtonState.Pressed)) { cancel();return; }
                button.Content=label+" · "+Math.Min(100,clock.ElapsedMilliseconds/10)+"%";
                if(gesture.Tick(clock.ElapsedMilliseconds)) { finished=true;timer.Stop();button.Content=label;if(button.IsMouseCaptured)button.ReleaseMouseCapture();advance(); }
            };
            button.PreviewMouseLeftButtonDown+=(s,e)=> { e.Handled=true;if(button.CaptureMouse())begin(true); };
            button.PreviewMouseLeftButtonUp+=(s,e)=> { e.Handled=true;cancel(); };
            button.PreviewKeyDown+=(s,e)=> { if(e.Key!=Key.Space&&e.Key!=Key.Return)return;e.Handled=true;if(!e.IsRepeat)begin(false); };
            button.PreviewKeyUp+=(s,e)=> { if(e.Key==Key.Space||e.Key==Key.Return) { e.Handled=true;cancel(); } };
            button.LostMouseCapture+=(s,e)=> { if(!finished)cancel(); };
            button.LostKeyboardFocus+=(s,e)=> { if(!finished)cancel(); };
            EventHandler deactivate=(s,e)=>cancel();window.Deactivated+=deactivate;
            button.Unloaded+=(s,e)=> { cancel();window.Deactivated-=deactivate; };
            return button;
        }
    }
}
