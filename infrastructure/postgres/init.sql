-- Create separate databases for each service (Database-per-Service pattern)
CREATE DATABASE b2b_identity;
CREATE DATABASE b2b_product;
CREATE DATABASE b2b_order;
CREATE DATABASE b2b_payment;
CREATE DATABASE b2b_shipping;
CREATE DATABASE b2b_vendor;
CREATE DATABASE b2b_discount;
CREATE DATABASE b2b_review;

-- Grant all privileges to b2b_user
GRANT ALL PRIVILEGES ON DATABASE b2b_identity TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_product TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_order TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_payment TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_shipping TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_vendor TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_discount TO b2b_user;
GRANT ALL PRIVILEGES ON DATABASE b2b_review TO b2b_user;
