
using MealPlaner.authentication;
using MealPlaner.CRUD;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Identity;
using MealPlaner.Models;
using MealPlaner.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateSlimBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var config = builder.Configuration;

builder.Services.AddRateLimiter(rateLimiterOptions =>
{

    rateLimiterOptions.AddPolicy("bucketPerUser", httpContext =>
    {
        // Extract the user id (sub) or subtier from the JWT
        var subtier = HeaderRequestDecoder.ExtractUserIdFromJwt(httpContext);

        // If subtier is found, use it as the partition key, otherwise fallback to IP address
        var partitionKey = subtier ?? httpContext.Connection.RemoteIpAddress?.ToString();

        // Return the rate limit partition based on the user
        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = (partitionKey =="premium") ? 10 : 5, // Maximum tokens (requests)
            TokensPerPeriod = 5, // Refill 5 tokens per period
            ReplenishmentPeriod = TimeSpan.FromSeconds(20), // Replenish tokens every 10 seconds
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0 // No queuing
        });
    });
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddTokenBucketLimiter("tokenBucket", options =>
    {
        options.TokenLimit = 5;
        options.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        options.TokensPerPeriod = 5;

    });

});


builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;

}).AddJwtBearer(x =>
{
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = config["jwtSettings:Issuer"],
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = config["jwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["jwtSettings:Key"]!)) //get from secure don't store in appsetings
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(CustomIdentityConstants.UserSubtierPolicyName, p => p.RequireClaim(CustomIdentityConstants.UserSubtierClaimName, "premium"));
});



builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
});

builder.Services.Configure<RecipesDatabaseSettings>(
    builder.Configuration.GetSection("RecipesDatabase"));

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
});



builder.Services.AddMemoryCache();
builder.Services.AddScoped<IRecipeCRUD, RecipeCRUD>();
builder.Services.AddScoped<IUserCRUD, UserCRUD>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<HeaderRequestDecoder>();
//builder.Services.AddScoped<APIKeyAuthFilter>();


var app = builder.Build();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//vrstni red ma veze 
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


