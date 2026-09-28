using System;
using UnityEngine;

internal static class ControlsViewChecks
{
    private sealed class Services : IIdas3ControlsServices
    {
        public Idas3ControlBindings Bindings { get; }
        public Idas3ControllerDevices Devices { get; }
        public Idas3GameOptions Options { get; } = new Idas3GameOptions();
        public Idas3WheelFeedback Feedback => null;
        public string Message => "";
        public bool SaveIncomplete => false;

        internal int saves, discards, leaves;
        internal Idas3ControlBindings.MenuActionId? menuAction;
        internal Idas3ControlBindings.ActionId? drivingAction;
        internal Services(Idas3ControlBindings bindings, Idas3ControllerDevices devices, string root)
        {
            Bindings = bindings;
            Devices = devices;
            Options.Initialize(root);
        }

        public void LogDevices()
        {
        }

        public void Save()
        {
            ++saves;
        }

        public void Discard()
        {
            ++discards;
        }

        public void Leave()
        {
            ++leaves;
        }

        public void Rebind(Idas3ControlBindings.ActionId action, Idas3ControlBindings.Slot slot)
        {
            drivingAction = action;
        }

        public void RebindMenu(Idas3ControlBindings.MenuActionId action)
        {
            menuAction = action;
        }

        public void AdjustFeedback(int row, int direction)
        {
        }

        public void SelectSavedController()
        {
        }
    }

    internal static int Run(Idas3ControlBindings bindings, Idas3ControllerDevices devices, string root)
    {
        int count = 0;
        void Check(bool value, string why)
        {
            ++count;
            if (!value)
                throw new Exception(why);
        }

        var services = new Services(bindings, devices, root);
        var view = new Idas3ControlsSetupView(services);
        void Draw(string click = null)
        {
            GUI.Click = click;
            GUI.Labels.Clear();
            GUI.Drawn.Clear();
            GUI.ScrollCalls = 0;
            view.Draw();
        }

        Draw();
        Check(GUI.Labels.Contains("Connected devices") && GUI.Labels.Contains("Keyboard"), "Devices renders provider list plus keyboard");
        Check(!GUI.Labels.Exists(s => s != null && (s.Contains("Draft") || s.Contains("Input: Multi"))), "normal Devices labels omit internal modes");
        Draw("Quick Setup");
        Draw();
        Check(view.Setup.State == Idas3SetupSession.Stage.Capturing, "Quick Setup starts axis capture immediately without device choice");
        Draw("CANCEL SETUP");
        Check(!view.Setup.Open && view.Page == 1, "visible cancel leaves wizard");
        Draw("Test Inputs");
        Check(view.Testing, "Test Inputs starts on entry");
        Draw();
        Check(GUI.Labels.Contains("Other Inputs") && GUI.Labels.Contains("Steering"), "Test Inputs retains evaluated steering and adds activity section");
        Check(GUI.Drawn.TrueForAll(item => item.rect.y >= 0 && item.rect.y + item.rect.height <= 680), "Test Inputs activity and exit controls remain inside the panel");
        Draw("Devices");
        Check(!view.Testing, "leaving Test Inputs stops test ownership");
        Draw("SAVE CHANGES");
        Draw("DISCARD CHANGES");
        Draw("BACK");
        Check(services.saves == 1 && services.discards == 1 && services.leaves == 1, "footer commands dispatch once through adapter");
        Draw("Bindings");
        Draw("MENU");
        Draw();
        // Focus after the group buttons starts at the selected Menu group.
        view.Navigate(1);
        view.Activate();
        Check(services.menuAction == Idas3ControlBindings.MenuActionId.Up, "menu row uses explicit menu binding service");
        Draw("Not assigned");
        view.Navigate(1);
        view.Activate();
        Check(services.menuAction == Idas3ControlBindings.MenuActionId.Down, "pointer row focus is retained when resuming keyboard navigation");
        foreach (string group in new[]
        {
            "DRIVING",
            "MENU",
            "KEYBOARD"
        }

        )
        {
            Draw(group);
            Draw();
            Check(GUI.ScrollCalls == 0, group + " has no scroll view");
            Check(GUI.Drawn.Exists(item => item.label == "SAVE CHANGES") && GUI.Drawn.Exists(item => item.label == "BACK"), group + " keeps footer visible");
            foreach (var size in new[]
            {
                (1280f, 720f),
                (862f, 569f)
            }

            )
            {
                float scale = Math.Min(1.5f, Math.Min(size.Item1 / 1072, size.Item2 / 704));
                Check(GUI.Drawn.TrueForAll(item => item.rect.x >= 0 && item.rect.x + item.rect.width <= 1040 && item.rect.y + item.rect.height <= 680), group + " bounds fit the game's logical panel");
                Check(GUI.Drawn.TrueForAll(item => item.font * scale >= 12.8f), group + " text remains at least 12.8 px at full game window " + size);
            }

            int rows = GUI.Labels.FindAll(label => label == "Gas" || label == "Brake" || label == "Steer left" || label == "Steer right" || label == "Shift up" || label == "Shift down" || label == "Change camera" || label == "Pause" || label == "Online menu" || label == "Headlights").Count;
            Check(rows == (group == "MENU" ? 0 : 10), group + " contains every driving action when appropriate");
            int menus = GUI.Labels.FindAll(label => Array.IndexOf(Idas3ControlBindings.MenuActionNames, label) >= 0 || label == "Open Options").Count;
            Check(menus == (group == "DRIVING" ? 0 : 8), group + " contains every menu action when appropriate");
        }

        view.Select(4);
        Draw();
        Check(GUI.Labels.Contains("Force-output device"), "FFB selector names output ownership");
        return count;
    }
}
