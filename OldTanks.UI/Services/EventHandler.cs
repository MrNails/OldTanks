namespace OldTanks.UI.Services;

public delegate void EventHandler<in TSender, in TArg>(TSender sender, TArg e);
