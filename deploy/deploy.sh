#!/bin/bash

az deployment group create \
        --name "$(date +'%Y%m%d-%H%M%S')" \
        --resource-group $1 \
        --template-file deploy.json