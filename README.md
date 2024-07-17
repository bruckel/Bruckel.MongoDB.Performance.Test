# Bruckel.MongoDB.Performance.Test

### Prueba de concepto de CRUDs de [Mongo Time Series](https://www.mongodb.com/docs/manual/core/timeseries-collections) en [Mongo Atlas](https://www.mongodb.com/docs/atlas/getting-started/) utilizando [Mongo Driver .NET](https://www.mongodb.com/docs/drivers/csharp/current/).
* Utiliza una imagen de Docker de Mongo Atlas.
  - Recomiendo instalar [Docker Desktop](https://www.docker.com/products/docker-desktop)
  - Seguir los pasos de la [documentación oficial](https://www.mongodb.com/docs/atlas/cli/current/atlas-cli-deploy-docker/#create-a-local-atlas-deployment-with-docker-compose).
  - Dentro de la carpeta Docker del repositortio se ubica el YAML que puede servir de base.
* Aplicación de consola .NET
  - Se puede depurar con Visual Studio 2022 o [Visual Studio Code](https://code.visualstudio.com/)
  - Crea una serie de datos temporales de granularidad horaria (un año) para 100 suministros. (El identificador es numérico, pero tambien hay lógica para crear CUPS aleatorios)
  - Se aplica un [Pipeline](https://www.mongodb.com/docs/manual/core/aggregation-pipeline/) y se copian los resultado sen una [Vista](https://www.mongodb.com/docs/manual/core/materialized-views/).
