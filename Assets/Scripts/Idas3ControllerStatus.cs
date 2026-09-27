using System;

// Presentation only: the shared bindings and saved options still own behavior.
internal static class Idas3ControllerStatus
{
    internal static string Configuration(bool savedMulti,bool draftMulti)=>
        "Active: "+(savedMulti?"Multi-input":"Existing controls")+" | Draft: "+(draftMulti?"Multi-input":"Existing controls")+" | Shared by CONTROLS + WHEEL";

    internal static bool FeedbackPending(Idas3GameOptions.Values saved,Idas3GameOptions.Values draft)=>
        saved.wheelForceFeedback!=draft.wheelForceFeedback||saved.wheelFeedbackStrength!=draft.wheelFeedbackStrength||
        saved.wheelFeedbackInvert!=draft.wheelFeedbackInvert||!string.Equals(saved.wheelFeedbackDevice,draft.wheelFeedbackDevice,StringComparison.Ordinal);
    internal static string Feedback(Idas3GameOptions.Values saved,Idas3GameOptions.Values draft,bool multiBlocked,string runtime){
        bool pending=FeedbackPending(saved,draft);
        string settings="Saved enable: "+(saved.wheelForceFeedback?"ON":"OFF")+". "+(pending?"Pending FFB changes — Save Changes to apply.":"FFB settings saved.");
        string output=multiBlocked?"Multi-input blocks force output; ownership is not validated.":"Backend: "+(runtime??"Output status unavailable.");
        return settings+"\n"+output+" Menu blocks motor output; no force test runs here.";
    }
}
