CREATE TABLE image (
    id uuid NOT NULL,
    preview bytea NOT NULL,
    body bytea NOT NULL,
    uploaded_at timestamp with time zone NOT NULL
);
