namespace Common.Infrastructure.Delegates;

public delegate void EventHandler<in TSender, in TArg>(TSender sender, TArg e);
