import json
import os
from pathlib import Path,PureWindowsPath

from Services.LoadDocumentsService import LoadDocumentsService


def callbackFile(ch, method, properties, body):
    data = json.loads(body.decode())
    bucket_name = data['BucketName']
    file_key = data['DocumentNames']
    print(f'Hello Admin, we received your message => {data} commandName => {bucket_name} file_key => {file_key}')
    print(f'file queue {body}')

    main_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    config_file_path = os.path.join(main_dir, 'config.ini')
    load_documents_service = LoadDocumentsService(config_file_path)

    filename = Path("BackEnd\DocuAurora_Python\DocuAurora\Model")

    # Convert path to Windows format
    path_on_windows = PureWindowsPath(filename)

    relative_path = os.path.abspath("BackEnd\DocuAurora_Python\DocuAurora\Model")


    print(f'{bucket_name} -- {file_key} --- {path_on_windows}')
    load_documents_service.download_files(bucket_name, file_key, path_on_windows)
    # load_documents_service.load_documents(model_abs_dir)

    print("DOWNLOAD DONE")
