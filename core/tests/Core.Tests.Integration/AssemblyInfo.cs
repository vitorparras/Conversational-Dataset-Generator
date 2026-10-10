// Every test in this project runs a real process; the quick gate filters them out by this trait.
[assembly: Trait("Category", "Integration")]

// Real processes share ports and, for the shutdown test, a console: one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
