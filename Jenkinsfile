pipeline {
    agent {
        node {
            label 'vs2022'
        }
    }
    options {
        copyArtifactPermission('/AD7166/Sign and Deploy SysConfigV2')
    }
    stages {
        stage('Build') {
            steps {
                powershell '''dotnet restore ConfigFileCreator/SystemConfigurationTool.csproj
        dotnet build -c Release --self-contained -r win-x64  ConfigFileCreator/SystemConfigurationTool.csproj'''
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
                    powershell 'dotnet publish ConfigFileCreator/SystemConfigurationTool.csproj -p:PublishProfile=FolderProfile /p:AssemblyVersion=$env:version /p:Version=$env:version'
                }
                archiveArtifacts artifacts: 'ConfigFileCreator/target/publish/**', followSymlinks: false
                build wait: false, job: '/AD7166/Sign and Deploy SysConfigV2', parameters: [string(name: 'gitTag', value: TAG_NAME)]
            }
        }
    }
    post {
        cleanup {
            cleanWs()
        }
    }
}
