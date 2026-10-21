// Οι δοκιμές WPF δημιουργούν το (μοναδικό ανά AppDomain) Application - η παράλληλη εκτέλεση κλάσεων δοκιμών έκανε το
// "if (Application.Current == null) new App()" race condition ("Cannot create more than one Application").
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
