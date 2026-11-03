-- What the derivation of a password left (the salt and the result) is read and replaced by the application's role and by no other. The 0006 granted it to PUBLIC, which reaches any role that
-- connects, and a role that can read the salt and the result can try passwords offline. PUBLIC loses everything on the table, column grants included, and the role app, the one that sets
-- and verifies passwords, receives exactly what PUBLIC had. Nobody deletes or empties the table, as the 0006 already settled.
-- A role of its own for verifying passwords, when there is one, takes this grant over by a migration of its own.
REVOKE ALL ON public.person_credential FROM PUBLIC;
GRANT SELECT ON public.person_credential TO app;
GRANT INSERT (person_id, algorithm, work_factor, salt, derived) ON public.person_credential TO app;
GRANT UPDATE (algorithm, work_factor, salt, derived, derived_at) ON public.person_credential TO app;
