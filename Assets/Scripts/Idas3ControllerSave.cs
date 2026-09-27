using System;

// Independent files cannot form an atomic transaction. Stop at the first failure and
// retain failed/not-attempted drafts. The caller retires Setup's checkpoint only after bindings save.
internal static class Idas3ControllerSave
{
    internal sealed class Result
    {
        internal bool Complete, BindingsSaved;
        internal string Message;
    }

    internal static Result Save(
        Func<bool> bindings, Func<string> bindingError,
        Func<bool> device, Func<string> deviceError,
        Func<bool> feedback, Func<string> feedbackError,
        Func<bool> experimental = null, Func<string> experimentalError = null)
    {
        var result = new Result();
        if (!bindings())
        {
            result.Message = "Nothing saved. Bindings: " + bindingError() + " Device and FFB remain pending. Retry Save Changes or Discard Changes.";
            return result;
        }

        result.BindingsSaved = true;
        if (experimental != null && !experimental())
        {
            result.Message = "Existing bindings saved. Multi-input: " + experimentalError() + " Device and FFB remain pending. Retry Save Changes or Discard Changes.";
            return result;
        }

        if (!device())
        {
            result.Message = "Bindings saved. Device: " + deviceError() + " FFB remains pending. Retry Save Changes or Discard Changes.";
            return result;
        }

        if (!feedback())
        {
            result.Message = "Bindings and device saved. FFB: " + feedbackError() + " Retry Save Changes or Discard Changes.";
            return result;
        }

        result.Complete = true;
        result.Message = "Controller bindings (including multi-input), selected device and FFB settings saved. Other option drafts kept.";
        return result;
    }
}
