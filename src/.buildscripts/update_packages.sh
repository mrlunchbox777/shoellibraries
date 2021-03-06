echo updating packages

cd src

for j in $(ls -d */)
do
    echo "----------------"
    echo "updating ${j}"
    echo "----------------"
    cd $j
    for i in $(cat *.csproj | grep PackageReference | cut -d'"' -f 2)
    do
        echo "updating ${i} for ${j}"
        dotnet add package $i
        echo "updated ${i} for ${j}"
    done
    cd ..
    echo "----------------"
    echo "updated ${j}"
    echo "----------------"
done

cd ..

echo updated!
