using UnityEngine;

// The view never polls hardware, writes files, or owns motor output. The game
// adapter supplies its existing services and commands through this boundary.
internal interface IIdas3ControlsServices
{
    Idas3ControlBindings Bindings { get; }

    Idas3ControllerDevices Devices { get; }

    Idas3GameOptions Options { get; }

    Idas3WheelFeedback Feedback { get; }

    string Message { get; }

    bool SaveIncomplete { get; }

    void LogDevices();
    void Save();
    void Discard();
    void Leave();
    void Rebind(Idas3ControlBindings.ActionId action, Idas3ControlBindings.Slot slot);
    void RebindMenu(Idas3ControlBindings.MenuActionId action);
    void AdjustFeedback(int row, int direction);
    void SelectSavedController();
}
