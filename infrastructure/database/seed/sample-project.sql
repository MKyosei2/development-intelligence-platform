INSERT INTO projects (
    "Id",
    "Name",
    "RepositoryUrl",
    "DefaultBranch",
    "HealthScore",
    "CreatedAtUtc",
    "UpdatedAtUtc")
SELECT
    gen_random_uuid(),
    'Sample Aurora',
    'https://github.com/example/aurora',
    'develop',
    100.00,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM projects WHERE "Name" = 'Sample Aurora'
);
