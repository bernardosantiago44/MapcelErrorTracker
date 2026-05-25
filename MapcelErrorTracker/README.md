## Prerequisites

| Tool | Min version | Recommended |
|------|:-----------:|:-----------:|
| Node |    24.16    |   Latest    |
| .NET |    10.0     |    10.0     | 

## Architecture overview

These diagrams are intentionally high-level. They show the current ASP.NET Core MVC structure and the main dependency direction without documenting every route, view, query, or model.

### C1 - System context

```mermaid
flowchart LR
    developer["Developer / maintainer"]
    browser["Web browser"]
    app["Mapcel Error Tracker<br/>ASP.NET Core MVC app"]
    database[("SQL Server<br/>MapaLocalizadorVisor")]
    console["Console logs<br/>Serilog"]

    developer -->|"Uses"| browser
    browser -->|"HTTPS requests"| app
    app -->|"Reads and updates errors, history, users, and assignments"| database
    app -.->|"Writes application logs"| console
```

### C2 - Container view

```mermaid
flowchart TB
    browser["Web browser"]
    database[("SQL Server<br/>MapaLocalizadorVisor")]

    subgraph app["Mapcel Error Tracker ASP.NET Core MVC app"]
        controllers["Controllers<br/>MVC pages and JSON APIs"]
        views["Razor views and static assets<br/>CSHTML, JavaScript, CSS"]
        services["Application services<br/>Error, occurrence, and user workflows"]
        config["Configuration<br/>appsettings and environment"]
        logging["Serilog logging"]
    end

    browser -->|"HTTPS"| controllers
    controllers -->|"Returns HTML"| views
    controllers -->|"Calls interfaces"| services
    services -->|"Microsoft.Data.SqlClient"| database
    services -->|"Reads connection strings"| config
    controllers -.->|"Logs request failures"| logging
    services -.->|"Logs data access failures"| logging
```

### C3 - Component overview

```mermaid
flowchart TB
    subgraph presentation["Presentation components"]
        errorsController["ErrorsController"]
        occurrenceController["ErrorOccurrenceController"]
        usersController["UsersController"]
        razorLayout["Shared Razor layout"]
    end

    subgraph contracts["Service contracts"]
        errorServiceContract["IErrorService"]
        metricServiceContract["IErrorOccurrenceMetricService"]
        usersServiceContract["IUsersService"]
    end

    subgraph services["Services"]
        errorService["ErrorService"]
        metricService["ErrorOccurrenceMetricService"]
        usersService["UsersService"]
        baseService["BaseService"]
        heatClassifier["ErrorHeatClassifier"]
        cssProvider["CssProvider"]
    end

    database[("SQL Server<br/>MapaLocalizadorVisor")]
    config["Configuration<br/>Connection strings"]

    errorsController --> errorServiceContract
    errorsController --> usersServiceContract
    occurrenceController --> metricServiceContract
    usersController --> usersServiceContract

    errorServiceContract -.-> errorService
    metricServiceContract -.-> metricService
    usersServiceContract -.-> usersService

    errorService --> baseService
    metricService --> baseService
    usersService --> baseService

    errorService --> heatClassifier
    metricService --> heatClassifier
    razorLayout --> cssProvider

    baseService -->|"Reads connection strings"| config
    errorService -->|"Errors, assignments, occurrence summaries"| database
    metricService -->|"Occurrence summaries and histograms"| database
    usersService -->|"Programmer users"| database
```

## Run the app

### Install tailwindcss using the CLI

Styling tool CSS wrapper: 

```bash
cd MapcelErrorTracker
npm install
npm run build:css
```

https://tailwindcss.com/

### Run a local version of the database

Run a local snapshot of the `MapaLocalizadorVisor` schema in Microsoft SQL Server.

### Run the application

Run the application on https. 
