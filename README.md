# Code Generator 3
Generate code for an ASP.NET Core 6 + Entity Framework Core website, using WebApi for a RESTFUL API service, and an Angular 14 font-end using TypeScript & Bootstrap 5.

To get started, just rename the `Web.Sample.config` and configure it to your environment. (*CodeGenerator runs on (old) ASP.NET v4, but the outputs are for modern (2022) targets, so don't be put off!*)

- Set the connection string
- Set your name, email and password so you can log in on first run
- Set the localhost url that the website will run on
- Run the app and add your first Project!

You will need **Website 3**, which you can find [here](https://github.com/capesean/Website3), as the project into which your generated code will be placed.

Creating a project automatically adds a `User` entity with fields for `Id`, `Email`, `First Name`, `Last Name`, `Full Name` and `Disabled`. (The `Full Name` is a database-calculated field.) 

![image](https://user-images.githubusercontent.com/642609/172637620-94836445-f8ab-4a94-bf27-3e23f8f77087.png)

You can immediately generate (and deploy) the code from this initial setup. The screenshot below shows the generated code for the `User` entity:

![image](https://user-images.githubusercontent.com/642609/172638876-e80e57be-ce36-4fcd-9221-a7a229cc3a40.png)

In the above screenshot, you can see the tabs for each of the different files that Code Generator 3 will produce, including:
- `Model`: creates the class which EFCore will use to generate a table in the database
- `TypeScriptModel`: creates the TypeScript model to work with in your front-end Angular app
- `Enums`: if you need to use Enums as fields
- `DTO`: The REST API converts your Model classes to DTOs before returning them to the webpage, allowing better control
- `SettingsDTO`: a generic settings DTO
- `DbContext`: creates part of the DBContext class for EF Core
- `Controller`: creates a WEB API controller for the RESTful API
- `BundleConfig`: legacy naming, but this creates the `generated.module.ts` and `shared.module.ts` files
- `AppRouter`: creates the `generated.routes.ts` file for Angular routing
- `ApiResource`: creates a typescript service file for each entity, e.g. `user.service.ts`
- `ListHtml`: creates the HTML for searching/listing each entity
- `ListTypeScript`: creates the TypeScript for searching/listing each entity
- `EditHtml`: creates the HTML for adding/editing/deleting each entity
- `EditTypeScript`: creates the TypeScript for adding/editing/deleting each entity
- `AppSelectHtml`: creates the selector directive for selecting entities from lists (think of it as an advanced TypeAhead)
- `AppSelectTypeScript`: creates the selector directive for selecting entities from lists (think of it as an advanced TypeAhead)
- `SelectModalHtml`: creates the modal directive for selecting entities from lists (think of it as an advanced TypeAhead)
- `SelectModalTypeScript`: creates the modal directive for selecting entities from lists (think of it as an advanced TypeAhead)

These outputs can be deployed directly into your target project if you have set up the `RootPath` value in your **web.config**, as well as the `WebPath` & `ModelsPath` values in the Project screen.

Of course, the generated code doesn't give you everything you need for a fully functional website. For that you need [Website 3](https://github.com/capesean/Website3).

To get **Website 3** to run, simply configure your `appsettings.json` and `environment.ts` files (samples provided). 

What you first run Website 3, you will be asked for a login (Name, Email, Password). Once you're logged in, you will be able to see the generated code as shown below:

The User search/list page:

![image](https://user-images.githubusercontent.com/642609/172645183-dd3a1aa8-1c39-454e-946b-d8be2bb5c87d.png)

The User add/edit/delete page:

![image](https://user-images.githubusercontent.com/642609/172645403-280459b4-dde1-490c-a738-6c143c096257.png)

Of course, there is more, such as adding relationships between entities, having hierarchical relationships, etc. The screenshot below, for example, shows how a searchable relationship between two entities is produced using a modal window that lets you search - in this case, searching the list of users.

![image](https://user-images.githubusercontent.com/642609/172646245-97cf2e4f-52b8-44d9-939b-c1532ed70f8b.png)

Feel free to get in touch for more assistance: [twitter.com/capesean](https://twitter.com/capesean)


