# PacsRefetch

PACS fetcher with retry/resume support and time limiting


Job:

Connect to a specified PACS, enumerate studies in a particular hour, fetch any which are not already present.

Output each hour to its own directory, of the form YYYYMMDDHH, with each DICOM object in a file (modality).(InstanceUID)

This enables rapid skipping of objects which have already been fetched.

