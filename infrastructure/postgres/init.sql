-- Create separate databases for each service (Database-per-Service pattern)
CREATE DATABASE b2b_identity;
CREATE DATABASE b2b_product;
CREATE DATABASE b2b_order;

-- Grant all privileges to b2b_user
GRANT ALL PRIVILEGES ON DATABASE b2b_identity TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_product TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_order TO b2b_user;
