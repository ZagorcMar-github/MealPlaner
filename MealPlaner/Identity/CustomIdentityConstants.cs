namespace MealPlaner.Identity
{
    /// <summary>
    /// Defines custom constants for identity claims and policies used for user authorization and role management.
    /// </summary>
    public class CustomIdentityConstants
    {
        /// <summary>
        /// The name of the claim representing the user's subscription tier.
        /// </summary>
        public const string UserSubtierClaimName = "subscription";

        /// <summary>
        /// The policy name associated with user subscription roles.
        /// </summary>
        public const string UserSubtierPolicyName = "Role";

        /// <summary>
        /// The name of the claim representing administrative privileges.
        /// </summary>
        public const string UserAdminClaimName = "admin";

        /// <summary>
        /// The policy name associated with administrative access.
        /// </summary>
        public const string UserAdminPolicyName = "Admin";

    }
}
