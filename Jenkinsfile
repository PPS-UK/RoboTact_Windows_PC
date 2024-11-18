pipeline {
    agent {
        node {
            label 'vs2022'
        }
    }
    options {
        copyArtifactPermission('/Customer Apps/Sign and Deploy RoboTact USB')
    }
    stages {
        stage('Build') {
            steps {
                powershell '''dotnet restore RoboTact/RoboTact.csproj
        dotnet build -c Release --self-contained -r win-x64  RoboTact/RoboTact.csproj'''
            }
        }

        stage('Publish') {
            when { tag 'v*' }
            steps {
                script {
                    final version = TAG_NAME[1..TAG_NAME.length() - 1]
                    env.Version = version
                }
                withEnv(['version =  $Version']) {
                    powershell 'echo $env:version'
                    powershell 'dotnet publish RoboTact/RoboTact.csproj -p:PublishProfile=FolderProfile /p:AssemblyVersion=$env:version /p:Version=$env:version'
                }
                archiveArtifacts artifacts: 'output/**', followSymlinks: false
                build wait: false, job: '/Customer Apps/Sign and Deploy RoboTact USB', parameters: [string(name: 'gitTag', value: TAG_NAME)]
            }
        }
    }
    post {
        cleanup {
            cleanWs()
        }
    }
}
