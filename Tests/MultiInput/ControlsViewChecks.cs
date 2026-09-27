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
            view.Draw();
        }

        Draw();
        Check(GUI.Labels.Contains("Connected devices") && GUI.Labels.Contains("Keyboard"), "Devices renders provider list plus keyboard");
        Check(!GUI.Labels.Exists(s => s != null && (s.Contains("Draft") || s.Contains("Input: Multi"))), "normal Devices labels omit internal modes");
        Draw("Quick Setup");
        Draw();
        Check(view.Setup.State == Idas3SetupSession.Stage.Choose, "Quick Setup enters choice immediately without Start screen");
        Draw("CANCEL SETUP");
        Check(!view.Setup.Open && view.Page == 1, "visible cancel leaves wizard");
        Draw("Test Inputs");
        Check(view.Testing, "Test Inputs starts on entry");
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
        view.Select(4);
        Draw();
        Check(GUI.Labels.Contains("Force-output device"), "FFB selector names output ownership");
        return count;
    }
}
