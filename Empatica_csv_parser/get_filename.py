import glob
subject_number = input("Input subject number (ex. subjectXX) : ")
all_filename_str = ""
for filename in glob.glob(subject_number+'*'):
    all_filename_str += filename + (' ')
    print(filename, end=' ')

with open('./filename_' + subject_number + '.txt', 'w') as fn_w:
    fn_w.write(all_filename_str)
