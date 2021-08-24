
export function dynamicSort<T>(property: keyof T) {
    var sortOrder = 1;
	let key = property as string

    if(typeof(key) === "string" && key[0] === "-") {
        sortOrder = -1;
        key = key.substr(1);
    }
    return function (a: T, b: T) {
        var result = (a[key] < b[key]) ? -1 : (a[key] > b[key]) ? 1 : 0;
        return result * sortOrder;
    }
}