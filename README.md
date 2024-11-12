Project Description

The Daily Meal Plan API is an automated solution for generating personalized daily/weakly/single meal plans.
This API is designed to help individuals, nutritionists,
or other meal planning apps by providing dynamically generated meal plans based on user preferences,
dietary restrictions, and nutritional goals.
With a focus on customization, the API enables users to receive meal suggestions that meet specific caloric and macronutrient
requirements while taking into account any food allergies or intolerances.


Data Accuracy Disclaimer

Please note that the data entries in this project are not 100% accurate. While we have made considerable efforts to exclude incorrect or inconsistent entries (esspecialy with alergens),
this process involved manual curation and may still contain minor inaccuracies. This dataset should be used with an understanding of these potential limitations.
We appreciate your understanding and recommend verifying critical data points if precision is necessary for your application.


data was prcoured from  the folowing sources:

https://www.kaggle.com/datasets/irkaal/foodcom-recipes-and-reviews

https://www.kaggle.com/datasets/realalexanderwei/food-com-recipes-with-ingredients-and-tags

Features

    Custom Meal Plans: Generate meal plans based on calories, macronutrient ratios (protein, carbs, fat)
    and dietary preferences such as vegetarian, vegan, keto, etc.
    Nutritional Tracking: Provides detailed nutritional breakdowns for each meal (calories, macronutrients, vitamins, etc.).
    Allergen Filtering: Allows users to exclude specific ingredients based on allergies or intolerances.
    Recipe Database: Includes a variety of recipes categorized by meal type (breakfast, lunch, dinner, snacks) that can be easily integrated into meal plans.
    Flexibility & Scaling: Plans can be created for a single day or scaled for longer periods (e.g., weekly or monthly plans).
    
    
    User Profiles: Store user preferences and past meal plans for personalized recommendations.
    
    Users may create new recipes
    Update recipes
    

Use Cases

    Personal Use: Individuals looking for healthy, tailored meal plans.
    Nutrition Apps: Fitness or wellness apps that need meal planning functionalities.
    Healthcare: Support for dietitians and nutritionists creating meal plans for patients.

Technologies Used

    Backend: asp.net
    Database: MongoDB for storing recipes and user data.
    Authentication: Supports JWT-based authentication
    Disclaimer: The authentication mechanism in this API is not fully secure and should not be used under any circumstances,
    as it was not the primary focus of the project. If authentication is needed, it is highly recommended to use a reliable external service.



recipeFormat:

    {
    "RecipeId":38,
    "Name":"Low-Fat Berry Blue Frozen Dessert",
    "CookTime":"PT24H",
    "PrepTime":"PT45M",
    "TotalTime":"PT24H45M",
    "RecipeCategory":"frozen desserts",
    "Keywords":["Dessert","Low Protein","Low Cholesterol","Healthy","Free Of...","Summer","Weeknight","Freezer","Easy","glutenFree","vegetarian","eggFree","nutFree","pescatarian","frozen desserts"],
    "RecipeIngredientParts":["blueberries","granulated sugar","vanilla yogurt","lemon juice"],
    "Calories":170.9,
    "FatContent":2.5,
    "SaturatedFatContent":1.3,
    "CholesterolContent":8.0,
    "SodiumContent":29.8,
    "CarbohydrateContent":37.1,
    "FiberContent":3.6,
    "SugarContent":30.2,
    "ProteinContent":3.2,
    "RecipeServings":4.0,
    "RecipeYield":null,
    "RecipeInstructions":["Toss 2 cups berries with sugar.","Let stand for 45 minutes, stirring occasionally.","Transfer berry-sugar mixture to food processor.","Add yogurt and process until smooth.","Strain through fine sieve. Pour into baking pan (or transfer to ice cream maker and process according to manufacturers' directions). Freeze uncovered until edges are solid but centre is soft.  Transfer to processor and blend until smooth again.","Return to pan and freeze until edges are solid.","Transfer to processor and blend until smooth again.","Fold in remaining 2 cups of blueberries.","Pour into plastic mold and freeze overnight. Let soften slightly to serve."],
    "ingredients_raw":["4   cups    blueberries, fresh or frozen ","1\/4  cup    granulated sugar","1   cup    vanilla yogurt, 1% or nonfat ","1   tablespoon    lemon juice"],
    "Calories_MinMax":0.0043297667,
    "FatContent_MinMax":0.0007167431,
    "SaturatedFatContent_MinMax":0.000837413,
    "CholesterolContent_MinMax":0.00026606,
    "SodiumContent_MinMax":0.0000258459,
    "CarbohydrateContent_MinMax":0.0045906751,
    "ProteinContent_MinMax":0.001122807,
    "FiberContent_MinMax":0.0025195969,
    "SugarContent_MinMax":0.0038624853
    }
Endpoints

Recipes
/api/Recipe/getRecipe/{id}


/api/Recipe/UpdateRecipe
    request body:
    {
      "name": "string",
      "cookTime": "string",
      "prepTime": "string",
      "totalTime": "string",
      "recipeCategory": "string",
      "keywords": [
        "string"
      ],
      "recipeIngredientParts": [
        "string"
      ],
      "totalCalories": 0,
      "totalFatContent": 0,
      "totalSaturatedFatContent": 0,
      "totalCholesterolContent": 0,
      "totalSodiumContent": 0,
      "totalCarbohydrateContent": 0,
      "totalFiberContent": 0,
      "totalSugarContent": 0,
      "totalProteinContent": 0,
      "recipeServings": 0,
      "recipeYield": "string",
      "recipeInstructions": [
        "string"
      ],
      "ingredients_raw": [
        "string"
      ],
      "recipeId": 0
    }

/api/Recipe/DeleteRecipe


/api/Recipe/CreateRecipe
    request body:
    [
      {
        "name": "string",
        "cookTime": "string",
        "prepTime": "string",
        "totalTime": "string",
        "recipeCategory": "string",
        "keywords": [
          "string"
        ],
        "recipeIngredientParts": [
          "string"
        ],
        "totalCalories": 0,
        "totalFatContent": 0,
        "totalSaturatedFatContent": 0,
        "totalCholesterolContent": 0,
        "totalSodiumContent": 0,
        "totalCarbohydrateContent": 0,
        "totalFiberContent": 0,
        "totalSugarContent": 0,
        "totalProteinContent": 0,
        "recipeServings": 0,
        "recipeYield": "string",
        "recipeInstructions": [
          "string"
        ],
        "ingredients_raw": [
          "string"
        ]
      }
    ]

/api/Recipe/GenerateMealPlan
    request body:
    {
      "preferences": [
        "string"
      ],
      "goals": {
        "targetCalories": 0,
        "targetFiberContent": 0,
        "targetFatContent": 0,
        "targetSaturatedFatContent": 0,
        "targetSugarContent": 0,
        "targetProteinContent": 0,
        "targetCarbohydrateContent": 0,
        "targetCholesterolContent": 0
      },
      "meals": {
        "additionalProp1": {
          "mustInclude": [
            "string"
          ],
          "mustExclude": [
            "string"
          ],
          "targetCalorieProcent": 0,
          "targetFiberProcent": 0,
          "targetFatProcent": 0,
          "targetSaturatedFatProcent": 0,
          "targetSugarProcent": 0,
          "targetProteinProcent": 0,
          "targetCarbohydrateProcent": 0,
          "targetCholesterolProcent": 0
        },
        "additionalProp2": {
          "mustInclude": [
            "string"
          ],
          "mustExclude": [
            "string"
          ],
          "targetCalorieProcent": 0,
          "targetFiberProcent": 0,
          "targetFatProcent": 0,
          "targetSaturatedFatProcent": 0,
          "targetSugarProcent": 0,
          "targetProteinProcent": 0,
          "targetCarbohydrateProcent": 0,
          "targetCholesterolProcent": 0
        },
        "additionalProp3": {
          "mustInclude": [
            "string"
          ],
          "mustExclude": [
            "string"
          ],
          "targetCalorieProcent": 0,
          "targetFiberProcent": 0,
          "targetFatProcent": 0,
          "targetSaturatedFatProcent": 0,
          "targetSugarProcent": 0,
          "targetProteinProcent": 0,
          "targetCarbohydrateProcent": 0,
          "targetCholesterolProcent": 0
        }
      }
    } 
    //"aditionalProp1/2/3" : are your given names of meals

/api/Recipe/getFilteredRecipes?page=&pageLimit=
    requestBody:
    {
      "desiredIngredientMatchPercentage": 0,
      "keywords": [
        "string"
      ],
      "ingredients": [
        "string"
      ],
      "excludeIngredients": [
        "string"
      ]


/api/Recipe/GetRecipesWithName


/api/Recipe/GetUniquePreferenceTypes


/api/Recipe/GetUniqueIngredients


USER:

/api/User/getUser/{id}


/api/User/getUserByUsername/{username}


/api/User/getUserByEmail/{email}


/api/User/UpdateUser
    request Body:{
      "age": 0,
      "username": "string",
      "email": "user@example.com",
      "password": "string",
      "heightCm": 0,
      "weightKg": 0,
      "name": "string",
      "subscription": "string",
      "admin": "string",
      "userId": 0,
      "previusRecipeIds": [
        0
      ]
    }

/api/User/UpdateUserRecipeIds?userId=
    request body:
    [
      0
    ]

/api/User/DeleteUser


/api/User/CreateUser
    request body:{
      "age": 0,
      "username": "string",
      "email": "user@example.com",
      "password": "string",
      "heightCm": 0,
      "weightKg": 0,
      "name": "string",
      "subscription": "string",
      "admin": "string"
    }

/api/User/AuthenticateUser
    request body:{
      "username": "string",
      "password": "string"
    }
