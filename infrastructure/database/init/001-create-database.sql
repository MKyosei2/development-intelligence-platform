SELECT 'CREATE DATABASE dip'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'dip')\gexec
