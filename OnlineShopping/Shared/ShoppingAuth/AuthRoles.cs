namespace ShoppingAuth;

public static class AuthRoles
{
    public const string Admin = "admin";

    public const string Manager = "Manager";

    public const string StoreCustomer = "Store customer";

    public const string AnyServiceUser = Manager + "," + StoreCustomer;
}
