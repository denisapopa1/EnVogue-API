# Wardrobe API - Architecture and Decisions

This document outlines the core architectural decisions and setup for the Wardrobe API.

## 1. Tech Stack
- **Framework:** .NET 9.0
- **Language:** C#
- **Database:** SQLite (local file `Wardrobe.db`)
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity with JWT Bearer tokens
- **Documentation:** Swagger (OpenAPI) with JWT support

## 2. Database & Data Storage
- **SQLite:** Chosen for simplicity and ease of portability.
- **Image Storage:** Images are NOT stored in the database as byte arrays. Instead:
  - Uploaded images are saved to a local folder: `wwwroot/uploads/images`.
  - The database stores the **relative path** to the image (e.g., `uploads/images/filename.jpg`).
  - Static files are enabled (`appUseStaticFiles()`) to allow the frontend to access these images.

## 3. Authentication & Seeding
- **Default Users:** The API automatically seeds 3 default users on startup:
  - `user1@example.com`
  - `user2@example.com`
  - `user3@example.com`
  - **Default Password:** `DefaultPassword123!`
- **JWT:** Authentication is handled via Bearer tokens. Claims include `NameIdentifier` (UserId) and `Email`.

## 4. Key Features
- **Wardrobe Management:** CRUD for clothing items with rich metadata (color, season, brand, tags).
- **Outfit Management:** Support for creating outfits from multiple clothing items (Many-to-Many).
- **Filtering & Sorting:** Robust endpoints for finding items by various criteria.
- **Outfit Generator:** A basic "AI" logic endpoint that suggests outfits based on color and occasion.
- **Favorites:** Ability to mark both items and outfits as favorites.

## 5. Developer Prerequisites
To run this project, you need:
- **.NET 9.0 SDK** or higher.
- **SQLite Browser** (optional, for inspecting the database).
- **Postman** or **cURL** (optional, if not using the built-in Swagger UI).

## 6. API Usage Flow (How to Login & Call APIs)

Since the API is protected by JWT, follow this flow to interact with the endpoints:

### Step 1: Login
Send a `POST` request to `/api/auth/login` with one of the default users:
- **Body (JSON):**
  ```json
  {
    "email": "user1@example.com",
    "password": "DefaultPassword123!"
  }
  ```
- **Response:** You will receive a JSON object containing a `"token"`.

### Step 2: Authenticate Requests
For all other requests (except registration and login), you must include the token in the **Authorization Header**:
- **Key:** `Authorization`
- **Value:** `Bearer <YOUR_TOKEN_HERE>`

### Step 3: Using Swagger
1. Open the browser to `http://localhost:<port>/swagger`.
2. Click the **"Authorize"** button at the top right.
3. Type `Bearer ` (with a space) followed by your token.
4. Click **"Authorize"** and then **"Close"**.
5. You can now use the "Try it out" feature on all protected endpoints.

## 7. IDE Support
The project is set up to work seamlessly with:
- **Visual Studio:** Open the `WardrobeApi.sln` file in the root directory.
- **Visual Studio Code:** Open the root folder. Press `F5` to start debugging using the provided `.vscode` configurations.

## 8. Setup Instructions
- **Run (CLI):** `dotnet run --project WardrobeApi` (Database will be created and seeded automatically).
- **Swagger:** Accessible at the root URL (e.g., `http://localhost:5207/`) when running in development.
- **Static Files:** Images can be accessed at `http://<host>:<port>/uploads/images/<filename>`.
