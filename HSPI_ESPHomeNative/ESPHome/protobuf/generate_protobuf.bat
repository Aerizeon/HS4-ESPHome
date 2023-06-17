cd bin
protoc -I="../" --csharp_out="../" "../esphome_api.proto" --csharp_opt=file_extension=.g.cs
protoc -I="../" --csharp_out="../" "../esphome_api_options.proto" --csharp_opt=file_extension=.g.cs